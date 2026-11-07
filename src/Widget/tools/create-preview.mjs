import { chromium } from 'playwright';
import { resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const browser = await chromium.launch({ channel: process.env.PLAYWRIGHT_CHANNEL || 'msedge', headless: true });
try {
    const page = await browser.newPage({ viewport: { width: 128, height: 56 } });
    const icon = pathToFileURL(resolve(root, 'resources/icon.svg')).href;
    await page.goto(pathToFileURL(resolve(root, 'index.html')).href);
    await page.setContent(`<html><body style="margin:0;background:#080f16;color:#e7f3f4;font-family:Segoe UI,sans-serif;display:flex;align-items:center;gap:8px;height:56px;padding:8px;box-sizing:border-box;border-left:3px solid #27d8bd"><img src="${icon}" style="width:22px;height:22px;background:#27d8bd;border-radius:3px"><div><strong style="font-size:13px">RTX Monitor</strong><div style="font-size:9px;color:#a5bdcb">POWER · PINS</div></div></body></html>`);
    await page.screenshot({ path: resolve(root, 'resources/preview.png') });
} finally { await browser.close(); }
