import assert from 'node:assert/strict';

export async function verifyGpuStatus(page, base, properties, transform) {
    await page.setViewportSize({ width: 840, height: 696 });
    await properties({ ...base, panelCount: 'One', panel1: 'nvidiaStatus', showHotspot: true, precision: '2' });
    await page.waitForFunction(() => document.querySelector('#panel1 .core-voltage-value')?.textContent.replaceAll(' ', '').includes('925.00mV'));
    assert.equal(await page.locator('#panel1 .status-pill').isVisible(), false);
    assert.equal(await page.locator('#panel1 [data-metric="temperatures"] .row-label').innerText(), 'Core / VRAM / HS');
    assert.match(await page.locator('#panel1 [data-metric="temperatures"] .row-value').innerText(), /62\.0.*70\.0.*73\.3/s);
    assert.equal(await page.locator('#panel1 [data-metric="fans"] .fan-duty').count(), 2);

    const unitTone = await page.locator('#panel1 .fan-duty .number').first().evaluate(node => ({
        color: getComputedStyle(node).color,
        expected: getComputedStyle(node.closest('.row-value').querySelector('.unit')).color
    }));
    assert.equal(unitTone.color, unitTone.expected, 'Fan duty uses unit coloring');

    await page.evaluate(() => {
        window.gpuStatusNodes = {
            voltage: document.querySelector('#panel1 .core-voltage-value .number'),
            fan: document.querySelector('#panel1 [data-metric="fans"] .fan-duty .number')
        };
    });
    await transform(frame => ({ ...frame, sensors: frame.sensors.map(reading => reading.identity.pluginId === 'generic-nvidia'
        ? reading.identity.component === 'GpuCore' && reading.identity.metric === 'Voltage' ? { ...reading, value: 1.025 }
            : reading.identity.component === 'GpuFan' && reading.identity.metric === 'FanDuty' ? { ...reading, value: 55 } : reading
        : reading) }));
    await page.waitForFunction(() => document.querySelector('#panel1 .core-voltage-value')?.textContent.includes('1025.00'));
    assert.ok(await page.evaluate(() => window.gpuStatusNodes.voltage === document.querySelector('#panel1 .core-voltage-value .number')
        && window.gpuStatusNodes.fan === document.querySelector('#panel1 [data-metric="fans"] .fan-duty .number')),
    'Sensor updates reuse the existing text nodes');

    await properties({ ...base, panelCount: 'One', panel1: 'nvidiaStatus', showFanDuty: false });
    assert.equal(await page.locator('#panel1 .fan-duty').count(), 0);
    assert.equal(await page.locator('#panel1 [data-metric="fans"] .row-label').innerText(), 'Fan 1 / 2');
    assert.equal(await page.locator('#panel1 [data-metric="temperatures"] .row-label').innerText(), 'Core / VRAM temp');
    await transform(frame => frame);
    await properties(base);
}
