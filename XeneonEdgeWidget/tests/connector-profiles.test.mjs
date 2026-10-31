import test from 'node:test';
import assert from 'node:assert/strict';
import { fixture, configuration, catalog } from './nvidia-fixtures.mjs';
import { parseSnapshot } from '../src/telemetry/parseSnapshot.ts';
import { TelemetryStore } from '../src/telemetry/TelemetryStore.ts';
import { Settings } from '../src/settings/Settings.ts';
import { Formatter } from '../src/formatting/Formatter.ts';
import { PeakStore } from '../src/telemetry/PeakStore.ts';
import { PanelPresenter } from '../src/panels/PanelPresenter.ts';

test('GPU status uses backend profile sources and does not fall back locally when unavailable', () => {
    const frame = parseSnapshot(fixture());
    const store = new TelemetryStore();
    store.configuration = configuration();
    store.configuration.configuration.monitoring.connectorProfiles = { version: 1 };
    store.catalog = catalog().plugins;
    store.connection = 'live';

    const power = frame.sensors.find(reading => reading.identity.pluginId === 'wireview-pro2'
        && reading.identity.component === 'GpuConnector' && reading.identity.metric === 'Power');
    power.value = 321;
    const current = frame.sensors.find(reading => reading.identity.pluginId === 'msi-psu'
        && reading.identity.component === 'PsuConnector' && reading.identity.metric === 'Current');
    current.value = 26.7;
    frame.selections = [power, current].map(reading => ({
        name: `gpu-status/${reading.identity.metric.toLowerCase()}`, state: 'Fallback', sensorKey: reading.identity.key
    }));
    store.update(frame);

    const settings = new Settings();
    settings.update(name => name === 'numberLocale' ? 'en-GB' : undefined);
    const presenter = new PanelPresenter(store, settings, new Formatter(settings), new PeakStore());
    const binding = { pluginId: 'generic-nvidia', deviceId: store.device('generic-nvidia', '', Date.parse(frame.timestamp)), kind: 'status' };
    const shown = presenter.present(binding, Date.parse(frame.timestamp));
    assert.match(shown.summary[0].text, /321/);
    assert.match(shown.summary[1].text, /26[.,]7/);
    assert.equal(shown.summary[2].text, '-');

    frame.selections = [];
    store.update(frame);
    assert.equal(presenter.present(binding, Date.parse(frame.timestamp)).summary[0].text, '-');
    assert.equal(presenter.present(binding, Date.parse(frame.timestamp) + 5001).summary[1].text, '-');
});
