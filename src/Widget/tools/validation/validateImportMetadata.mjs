import assert from 'node:assert/strict';
import { runInNewContext } from 'node:vm';
import { SaxesParser } from 'saxes';

// iCUE reads the head through QDomDocument, before Chromium loads the page.
// Browser HTML error recovery and the CLI's checks do not validate this format.
export function validateImportMetadata(html, localization) {
    const end = html.indexOf('</head>');
    assert.ok(end >= 0, 'Widget must contain a complete head element.');
    const translations = localization?.en?.translation;
    assert.ok(translations && typeof translations === 'object', 'translation.json must contain en.translation.');
    const evaluate = expression => runInNewContext(expression, { tr: key => {
        assert.equal(typeof translations[key], 'string', `Missing English translation: ${key}`);
        return translations[key];
    } }, { timeout: 100 });
    const parser = new SaxesParser();
    const stack = [];
    const assets = [];
    let titleExpression = '';
    let groupsJson = '';
    let readingGroups = false;
    parser.on('error', error => { throw new Error(`iCUE metadata must be valid XML: ${error.message}`); });
    parser.on('opentag', ({ name, attributes }) => {
        stack.push(name);
        if (name === 'link' && attributes.rel === 'icon') assets.push(attributes.href);
        if (name === 'meta') {
            if (attributes.name === 'x-icue-widget-preview') assets.push(attributes.content);
            if (attributes.name === 'x-icue-widget-group') evaluate(attributes.content);
            if (attributes.name === 'x-icue-property') {
                for (const attribute of ['data-label', 'data-default', 'data-values', 'data-unit-label']) {
                    if (attributes[attribute]) evaluate(attributes[attribute]);
                }
            }
        }
        if (name === 'script' && attributes.id === 'x-icue-groups') readingGroups = true;
    });
    parser.on('text', text => {
        if (stack.at(-1) === 'title') titleExpression += text;
        if (readingGroups) groupsJson += text;
    });
    parser.on('closetag', ({ name }) => {
        if (name === 'script') readingGroups = false;
        stack.pop();
    });
    parser.write(html.slice(0, end + '</head>'.length) + '</html>').close();
    assert.ok(titleExpression.trim(), 'Missing title element.');
    const title = evaluate(titleExpression);
    assert.ok(typeof title === 'string' && title.trim(), 'Widget title must resolve to a nonempty string.');
    assert.equal(assets.length, 2, 'Widget icon and preview must be declared.');
    for (const group of JSON.parse(groupsJson)) evaluate(group.title);
    return { title, assets };
}
