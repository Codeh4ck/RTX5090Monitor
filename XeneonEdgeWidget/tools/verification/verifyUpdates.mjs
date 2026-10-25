import assert from 'node:assert/strict';
import { join } from 'node:path';

export async function verifyUpdates(page, base, properties, setManifest, fail, requests, output) {
    await page.setViewportSize({ width: 1688, height: 696 });
    await properties({ ...base, checkUpdates: false, showAlerts: false });
    await page.evaluate(() => {
        window.openedUpdateLinks = [];
        window.plugins = { Linkprovider: { open: url => window.openedUpdateLinks.push(url) } };
    });

    setManifest({ schemaVersion: 1, releaseTag: 'v1.1.0', desktop: '1.1.0', service: '1.1.0', widget: '1.1.0' });
    const before = requests.filter(request => request.url.includes('api.github.com')).length;
    await properties({ ...base, checkUpdates: false, updateFrequency: 'Monthly', showAlerts: false });
    assert.equal(requests.filter(request => request.url.includes('api.github.com')).length, before);
    await page.locator('#panel1 .panel-title').dblclick();
    await page.locator('.check-updates').click();
    await page.getByRole('button', { name: 'View release', exact: true }).waitFor();
    assert.equal(await page.locator('.widget-flyout').getAttribute('data-mode'), 'updates');
    assert.match(await page.locator('.flyout-body').innerText(), /1.0.0[\s\S]*1.1.0/);
    await page.getByRole('button', { name: 'View release', exact: true }).click();
    assert.deepEqual(await page.evaluate(() => window.openedUpdateLinks),
        ['https://github.com/Codeh4ck/RTX5090Monitor/releases/tag/v1.1.0']);
    await page.screenshot({ path: join(output, 'L-H-updates.png') });
    await page.getByRole('button', { name: 'Skip this version', exact: true }).click();
    assert.equal(await page.locator('.update-notice').count(), 0);
    assert.match(await page.evaluate(() => localStorage.getItem('rtx-monitor-updates/default')), /"skipped":true/);
    await page.locator('.flyout-close').click();

    setManifest({ schemaVersion: 1, releaseTag: 'v1.2.0', desktop: '1.2.0', service: '1.2.0', widget: '1.2.0' });
    await page.locator('#panel1 .panel-title').dblclick();
    await page.locator('.check-updates').click();
    await page.getByRole('button', { name: 'View release', exact: true }).waitFor();
    assert.match(await page.locator('.flyout-body').innerText(), /1.2.0/);
    await page.setViewportSize({ width: 840, height: 344 });
    const bounds = await page.locator('.widget-flyout').boundingBox();
    assert.ok(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= 840 && bounds.y + bounds.height <= 344);
    await page.screenshot({ path: join(output, 'S-H-updates.png') });
    await page.getByRole('button', { name: 'Remind me later', exact: true }).click();
    assert.equal(await page.locator('.update-notice').count(), 0);

    fail(true);
    await page.getByRole('button', { name: 'Check now', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('.flyout-body').textContent.includes('Could not check for updates'));
    await page.locator('.flyout-close').click();
    fail(false);
    await page.evaluate(() => {
        window.uniqueId = 'update-verification-instance';
        icueEvents.onICUEInitialized();
    });
    await page.locator('#panel1 .panel-title').dblclick();
    await page.locator('.check-updates').click();
    await page.getByRole('button', { name: 'View release', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Skip this version', exact: true }).click();
    assert.match(await page.evaluate(() => localStorage.getItem('rtx-monitor-updates/update-verification-instance')), /"skipped":true/);
    assert.ok(await page.evaluate(() => localStorage.getItem('rtx-monitor-updates/default')));
    await page.locator('.flyout-close').click();
}
