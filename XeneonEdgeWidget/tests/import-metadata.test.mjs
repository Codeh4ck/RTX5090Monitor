import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { validateImportMetadata } from '../tools/validation/validateImportMetadata.mjs';

const localization = { en: { translation: { 'RTX Monitor': 'RTX Monitor', Settings: 'Settings' } } };
const header = `<!DOCTYPE html>
<html lang="en"><head>
    <meta charset="utf-8" />
    <title>tr('RTX Monitor')</title>
    <link rel="icon" href="resources/icon.svg" />
    <meta name="x-icue-widget-preview" content="resources/preview.png" />
    <script type="application/json" id="x-icue-groups">[{"title":"tr('Settings')","properties":[]}]</script>
</head><body><script>if (1 < 2) console.log('normal HTML runtime');</script></body></html>`;

test('import metadata resolves the title and packaged assets without parsing body JavaScript as XML', () => {
    assert.deepEqual(validateImportMetadata(header, localization), {
        title: 'RTX Monitor', assets: ['resources/icon.svg', 'resources/preview.png']
    });
});

test('rejects the lowercase doctype that prevented iCUE from reading pre5 metadata', () => {
    assert.throws(() => validateImportMetadata(header.replace('<!DOCTYPE', '<!doctype'), localization), /valid XML/);
});

test('rejects HTML void tags that hide the title from the XML metadata parser', () => {
    assert.throws(() => validateImportMetadata(header.replace('<meta charset="utf-8" />', '<meta charset="utf-8">'), localization), /valid XML/);
});

test('rejects flat translation maps and missing title translation keys', () => {
    assert.throws(() => validateImportMetadata(header, localization.en.translation), /en.translation/);
    assert.throws(() => validateImportMetadata(header, { en: { translation: { Settings: 'Settings' } } }), /Missing English translation: RTX Monitor/);
});

test('rejects a missing title or untranslated group', () => {
    assert.throws(() => validateImportMetadata(header.replace("<title>tr('RTX Monitor')</title>", ''), localization), /Missing title/);
    assert.throws(() => validateImportMetadata(header, { en: { translation: { 'RTX Monitor': 'RTX Monitor' } } }), /Missing English translation: Settings/);
});

test('source template retains XML-compatible iCUE head metadata', async () => {
    const template = await readFile(new URL('../index.html', import.meta.url), 'utf8');
    const html = template.replace('<!-- ICUE_PROPERTIES -->', '<script id="x-icue-groups">[]</script>');
    assert.equal(validateImportMetadata(html, localization).title, 'RTX Monitor');
});
