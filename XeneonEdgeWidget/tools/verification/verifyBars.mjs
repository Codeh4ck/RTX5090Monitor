import assert from 'node:assert/strict';
import { join } from 'node:path';

export async function verifyBars(page, base, properties, setFrame, output) {
    await page.setViewportSize({ width: 2536, height: 696 });
    const fill = page.locator('#panel1 .pin-fill').first();
    const number = '#panel1 .pin-value .number';
    const current = value => frame => ({ ...frame, sensors: frame.sensors.map(reading => ['GpuPin', 'PsuPin'].includes(reading.identity.component)
        && reading.identity.metric === 'Current' ? { ...reading, value } : reading) });
    const height = () => fill.evaluate(element => ({ actual: element.getBoundingClientRect().height / element.parentElement.getBoundingClientRect().height * 100,
        target: parseFloat(element.style.getPropertyValue('--fill')), duration: getComputedStyle(element).transitionDuration }));
    try {
        setFrame(current(.4));
        for (const [precision, expected] of [['0', '0'], ['1', '0.4'], ['2', '0.40'], [2, '0.40']]) {
            await properties({ ...base, precision, panel3: 'msiConnector1' });
            assert.equal(await page.locator(number).first().textContent(), expected, 'Decimal dropdown enforces trailing zeros');
            assert.equal(await page.locator('#panel1 .psu-comparison .number').first().textContent(), expected, 'PSU comparisons use exact precision');
            assert.equal(await page.locator('#panel3 .pin-value .number').first().textContent(), expected, 'MSI pin values use exact precision');
        }
        await properties({ ...base, precision: '2', panel1: 'wireviewPins', numberLocale: 'de-DE', showUnits: false });
        assert.equal(await page.locator('#panel1 .pin-value').first().textContent(), '0,40', 'Fixed decimals work on WireView without units and with a decimal comma');
        assert.equal(await page.locator('meta[name="x-icue-property"][content="barSmoothing"]').count(), 0);
        await properties({ ...base, barRadius: 50 });
        const corners = await fill.evaluate(element => ({ fillRadius: getComputedStyle(element).borderRadius,
            trackRadius: parseFloat(getComputedStyle(element.parentElement).borderTopLeftRadius), clip: getComputedStyle(element.parentElement).overflow }));
        assert.equal(corners.fillRadius, '0px', 'The fill has flat free edges');
        assert.ok(corners.trackRadius > 20, 'Container rounding remains configurable');
        assert.equal(corners.clip, 'hidden', 'The track clips only touching fill corners');
        setFrame(frame => ({ ...frame, sensors: frame.sensors.map(reading => reading.identity.component === 'GpuPin'
            && reading.identity.metric === 'Current' ? { ...reading, value: [.05, 2, 8, 10, 0, 10.5][reading.identity.channel - 1] } : reading) }));
        await page.waitForFunction(() => document.querySelector('#panel1 .pin-fill').style.getPropertyValue('--fill') === '0.5%');
        await page.screenshot({ path: join(output, 'XL-H-bar-corners.png') });
        setFrame(current(2));
        await properties(base);
        await page.waitForFunction(() => document.querySelector('#panel1 .pin-fill').style.getPropertyValue('--fill') === '20%');
        assert.ok(Math.abs((await height()).actual - 20) < .1, 'Bars update immediately');
        assert.equal((await height()).duration, '0s', 'There is no smoothing transition');
        setFrame(current(8));
        await page.waitForFunction(selector => document.querySelector(selector).textContent === '8.0', number);
        assert.ok(Math.abs((await height()).actual - 80) < .1, 'New readings apply without animation');
        setFrame(frame => ({ ...frame, plugins: frame.plugins.map(plugin => plugin.pluginId === 'asus-astral' ? { ...plugin, state: 'Error' } : plugin) }));
        await page.waitForFunction(() => document.querySelector('#panel1 .pin').dataset.available === 'false');
        assert.equal((await height()).actual, 0, 'Unavailable data clears immediately');
        assert.equal(await page.locator('#panel1 .pin-value').first().textContent(), '-');
        const metadata = await page.locator('meta[name="x-icue-property"][content="precision"]').evaluate(element => ({
            type: element.getAttribute('data-type'), value: element.getAttribute('data-default'), choices: element.getAttribute('data-values'), label: element.getAttribute('data-label') }));
        assert.equal(metadata.type, 'combobox');
        assert.equal(metadata.value, '"1"');
        assert.ok(metadata.label.includes('Decimal places'));
        for (const choice of ['0', '1', '2']) assert.ok(metadata.choices.includes(`key:"${choice}"`));
    } finally {
        setFrame(frame => frame);
        await properties(base);
    }
}
