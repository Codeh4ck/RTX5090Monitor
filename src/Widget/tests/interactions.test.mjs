import test from 'node:test';
import assert from 'node:assert/strict';
import { Settings } from '../src/settings/Settings.ts';
import { Formatter } from '../src/formatting/Formatter.ts';
import { TelemetryStore } from '../src/telemetry/TelemetryStore.ts';
import { parseSnapshot } from '../src/telemetry/parseSnapshot.ts';
import { WarningPresenter } from '../src/warnings/WarningPresenter.ts';
import { WarningTracker } from '../src/warnings/WarningTracker.ts';
import { previewWarnings, warningOptions } from '../src/warnings/previewWarnings.ts';
import { configurationRows } from '../src/configuration/configurationRows.ts';
import { PanelPresenter } from '../src/panels/PanelPresenter.ts';
import { resolvePanel } from '../src/panels/resolvePanel.ts';
import { PeakStore } from '../src/telemetry/PeakStore.ts';
import { fixture, configuration } from './fixtures.mjs';

test('clock and RPM grouping stays disabled in every locale, even with units hidden', () => {
    const settings = new Settings();
    const format = new Formatter(settings);
    for (const locale of ['en-GB', 'de-DE', 'fr-FR']) {
        for (const showUnits of [true, false]) {
            settings.update(name => ({ numberLocale: locale, showUnits })[name]);
            format.configure('°C');
            assert.equal(format.value(1560, 'MHz', 0), showUnits ? '1560 MHz' : '1560');
            assert.equal(format.value(14001, 'RPM', 0), showUnits ? '14001 RPM' : '14001');
            assert.notEqual(format.number(14001, 0), '14001', 'Other metrics retain their locale grouping');
        }
    }
});

test('configuration rows preserve false, zero, automatic devices and source-selection detail', () => {
    const rows = configurationRows({ enabled: false, pollIntervalMs: 1000, options: { deviceId: null, fanDutyPercent: 0,
        wireOvercurrentAmperageThreshold: 9.199999809 }, selections: [{ name: 'power', allowFallback: true }] });
    assert.deepEqual(rows.slice(0, 5).map(row => row.value), ['Disabled', '1000', 'Automatic', '0', '9.2']);
    assert.ok(rows.some(row => row.label === 'Selections 1 / Allow Fallback' && row.value === 'Enabled'));
});

function warningState(overrides = {}) {
    return { id: 'asus/core', pluginId: 'asus-astral', deviceId: '/gpu-nvidia/0', typeId: 'asus-astral/core-temperature', icon: 'temperature', title: 'GPU core temperature',
        detail: '81 °C', phase: 'Active', episodeId: 'first', startedAt: new Date().toISOString(), lastNotificationAt: new Date().toISOString(),
        policy: { notifyOnStart: true, repeatWhileActive: true, repeatIntervalSeconds: 5, clearAfterSeconds: 2 }, notificationSequence: 1, ...overrides };
}

test('warning reminders follow server sequences rather than values or affected pins', () => {
    const tracker = new WarningTracker();
    const feed = { instanceId: 'server', sequence: 1, notificationsEnabled: true, states: [warningState()] };
    assert.equal(tracker.update(feed), true);
    assert.equal(tracker.update({ ...feed, states: [warningState({ detail: '82 °C' })] }), false);
    assert.equal(tracker.update({ ...feed, sequence: 2, states: [warningState({ notificationSequence: 2 })] }), true);
    assert.equal(tracker.update({ ...feed, states: [warningState({ phase: 'Unavailable', notificationSequence: 3 })] }), false);
    assert.equal(tracker.update({ ...feed, states: [] }), false);
    assert.equal(tracker.update({ ...feed, states: [warningState({ episodeId: 'next' })] }), true);
    assert.equal(tracker.update(feed, false), false);
    assert.equal(tracker.update(feed, true), true);
});

test('warning previews cover all choices without modifying live telemetry', () => {
    const snapshot = fixture();
    const before = structuredClone(snapshot);
    assert.deepEqual(previewWarnings('none'), []);
    assert.deepEqual(previewWarnings('unknown'), []);
    for (const { key } of warningOptions.filter(option => option.key !== 'none')) {
        const warnings = previewWarnings(key);
        assert.ok(warnings.length >= 1);
        assert.ok(warnings.every(warning => warning.id.startsWith('preview/')));
    }
    assert.equal(previewWarnings('electrical').length, 2);
    assert.equal(previewWarnings('thermal').length, 2);
    assert.deepEqual(snapshot, before);
});

test('warning presentation consumes server state and does not invent local temperature triggers', () => {
    const frame = fixture();
    const now = Date.parse(frame.timestamp);
    frame.sensors.find(reading => reading.identity.component === 'GpuCore' && reading.identity.metric === 'Temperature').value = 110;
    const store = new TelemetryStore();
    store.update(parseSnapshot(frame));
    store.configuration = configuration();
    store.connection = 'live';
    const presenter = new WarningPresenter(store);
    assert.equal(presenter.present(now).length, 0);
    frame.warnings = { instanceId: 'server', sequence: 1, notificationsEnabled: true, states: [warningState({ startedAt: frame.timestamp })] };
    store.update(parseSnapshot(frame));
    assert.equal(presenter.present(now).length, 1);
    assert.equal(presenter.present(now)[0].detail, '81 °C');
    assert.ok(presenter.present(now + 5001).some(warning => warning.title.includes('Data unavailable')));
    frame.warnings.states = [];
    store.update(parseSnapshot(frame));
    assert.equal(presenter.present(now).length, 0);
});

test('pin panels do not prepare status rows, disabled summaries or disabled chart features', () => {
    const frame = fixture();
    const now = Date.parse(frame.timestamp);
    const store = new TelemetryStore();
    store.update(parseSnapshot(frame));
    store.configuration = configuration();
    store.connection = 'live';
    const settings = new Settings();
    settings.update(name => ({ panel1: 'asusPins', showHeadroom: false, showAxis: false, showSpread: false })[name]);
    const format = new Formatter(settings);
    const units = [];
    const value = format.value.bind(format);
    format.value = (...args) => { units.push(args[1]); return value(...args); };
    const data = new PanelPresenter(store, settings, format, new PeakStore()).present(resolvePanel(1, settings, store, now), now, false);
    assert.deepEqual(data.summary, []);
    assert.deepEqual(data.rows, []);
    assert.deepEqual(data.axis, []);
    assert.equal(data.spread, '-');
    assert.ok(data.pins.every(pin => pin.headroomValue === null && pin.peak === null));
    assert.equal(units.includes('MHz'), false);
    assert.equal(units.includes('RPM'), false);
});


test('unknown plugin warning types are displayed using service metadata', () => {
    const frame = fixture();
    const now = Date.parse(frame.timestamp);
    frame.warnings = { instanceId: 'third-party-service', sequence: 1, notificationsEnabled: true,
        states: [warningState({ typeId: 'vendor/new-condition', title: 'Vendor condition', icon: 'temperature', startedAt: frame.timestamp })] };
    const store = new TelemetryStore();
    store.update(parseSnapshot(frame));
    store.connection = 'live';
    const notice = new WarningPresenter(store).present(now).find(item => item.title === 'Vendor condition');
    assert.equal(notice.icon, 'vtemp');
    assert.equal(new WarningTracker().update(store.snapshot.warnings), true);
});
