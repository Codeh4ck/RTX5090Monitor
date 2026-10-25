import test from 'node:test';
import assert from 'node:assert/strict';
import { UpdateChecker } from '../src/updates/UpdateChecker.ts';
import { UpdateStateStore } from '../src/updates/UpdateStateStore.ts';
import { isNewerVersion, nextUpdateCheck, parseReleaseManifest, parseVersion, latestReleaseApi, rawReleaseRoot } from '../src/updates/versions.ts';

function setup(storage = new Map()) {
    const values = { schemaVersion: 1, releaseTag: 'v1.1.0', desktop: '1.1.0', service: '1.1.0', widget: '1.1.0' };
    let now = Date.UTC(2026, 0, 31, 12);
    let fail = false;
    let changes = 0;
    const requests = [];
    const store = new UpdateStateStore('widget-test', {
        getItem: key => storage.get(key) ?? null,
        setItem: (key, value) => storage.set(key, value)
    });
    const request = async (url, options) => {
        requests.push(String(url));
        assert.equal(options.signal.aborted, false);
        if (fail) throw new Error('offline');
        if (url === latestReleaseApi) return Response.json({ tag_name: values.releaseTag, draft: false, prerelease: false });
        assert.equal(url, `${rawReleaseRoot}${values.releaseTag}/version.json`);
        return Response.json(values);
    };
    const create = () => new UpdateChecker('1.0.0', store, () => changes++, request, () => now);

    return { values, requests, store, create, get changes() { return changes; },
        advance: milliseconds => now += milliseconds, fail: value => fail = value };
}

test('update versions compare numerically and reject malformed manifests', () => {
    assert.equal(isNewerVersion('1.10.0', '1.9.9'), true);
    assert.equal(isNewerVersion('1.0.0', '1.0.0'), false);
    assert.equal(isNewerVersion('1.0.0', '2.0.0'), false);
    for (const value of ['01.0.0', '1.0', '1.0.0-beta', '1.0.0+test', '-1.0.0', '9999999999.0.0']) assert.equal(parseVersion(value), null);
    assert.throws(() => parseReleaseManifest({ schemaVersion: 1, releaseTag: 'https://example.com' }));
});

test('monthly update scheduling clamps month ends and weekly uses seven days', () => {
    const now = Date.UTC(2026, 0, 31, 12);
    assert.equal(nextUpdateCheck(now, 'Daily'), now + 86400000);
    assert.equal(nextUpdateCheck(now, 'Weekly'), now + 7 * 86400000);
    assert.equal(nextUpdateCheck(now, 'Monthly'), Date.UTC(2026, 1, 28, 12));
});

test('widget startup always checks enabled updates and persists scheduled check state', async () => {
    const fixture = setup();
    const first = fixture.create();
    first.configure(true, 'Weekly');
    await first.check();
    assert.equal(first.availableVersion, '1.1.0');
    assert.equal(first.releaseUrl, 'https://github.com/Codeh4ck/RTX5090Monitor/releases/tag/v1.1.0');
    await first.check();
    assert.equal(fixture.requests.length, 2);
    fixture.advance(7 * 86400000);
    await first.check();
    assert.equal(fixture.requests.length, 4);
    const restarted = fixture.create();
    assert.notEqual(restarted.lastChecked, 'Never');
    await restarted.check();
    assert.equal(fixture.requests.length, 6);
});

test('disabling automatic widget checks permits explicit manual checks only', async () => {
    const fixture = setup();
    const checker = fixture.create();
    checker.configure(false, 'Daily');
    await checker.check();
    assert.equal(fixture.requests.length, 0);
    await checker.check(true);
    assert.equal(fixture.requests.length, 2);
    assert.equal(checker.availableVersion, '1.1.0');
});

test('widget skip persists across reload and a newer release is offered', async () => {
    const fixture = setup();
    const checker = fixture.create();
    await checker.check();
    checker.dismiss(true);
    assert.equal(checker.availableVersion, null);
    const restarted = fixture.create();
    await restarted.check();
    assert.equal(restarted.availableVersion, null);
    fixture.values.releaseTag = 'v1.2.0';
    fixture.values.widget = '1.2.0';
    fixture.advance(86400000);
    await restarted.check();
    assert.equal(restarted.availableVersion, '1.2.0');
});

test('widget reminders expire after 24 hours without an unnecessary weekly request', async () => {
    const fixture = setup();
    const checker = fixture.create();
    checker.configure(true, 'Weekly');
    await checker.check();
    checker.dismiss(false);
    assert.equal(checker.availableVersion, null);
    fixture.advance(86400000);
    await checker.check();
    assert.equal(checker.availableVersion, '1.1.0');
    assert.equal(fixture.requests.length, 2);
});

test('offline widget checks back off, allow manual retry and tolerate unavailable storage', async () => {
    const fixture = setup();
    fixture.fail(true);
    const checker = fixture.create();
    await checker.check();
    await checker.check();
    assert.equal(fixture.requests.length, 1);
    assert.match(checker.status, /Could not check/);
    assert.equal(checker.checking, false);
    fixture.fail(false);
    await checker.check(true);
    assert.equal(checker.availableVersion, '1.1.0');
    const blocked = new UpdateStateStore('blocked', { getItem() { throw new Error('denied'); }, setItem() { throw new Error('denied'); } });
    assert.equal(blocked.load().manifest, null);
    assert.doesNotThrow(() => blocked.save(fixture.store.load()));
});

test('widget rejects a mismatched release manifest and stops in-flight checks', async () => {
    let resolve;
    const pending = new Promise(done => resolve = done);
    const store = new UpdateStateStore('stopped', null);
    const checker = new UpdateChecker('1.0.0', store, () => {}, () => pending);
    const check = checker.check();
    checker.stop();
    resolve(Response.json({ tag_name: 'v1.1.0', draft: true, prerelease: false }));
    await check;
    assert.equal(checker.availableVersion, null);
    let step = 0;
    const mismatch = new UpdateChecker('1.0.0', store, () => {}, async () => step++ === 0
        ? Response.json({ tag_name: 'v1.1.0', draft: false, prerelease: false })
        : Response.json({ schemaVersion: 1, releaseTag: 'v1.2.0', desktop: '1.2.0', service: '1.2.0', widget: '1.2.0' }));
    await mismatch.check();
    assert.equal(mismatch.availableVersion, null);
    assert.equal(mismatch.lastChecked, 'Never');
});
