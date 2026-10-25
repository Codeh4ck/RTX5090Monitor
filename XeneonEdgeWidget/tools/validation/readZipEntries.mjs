import assert from 'node:assert/strict';
import { inflateRawSync } from 'node:zlib';

// Read the CLI's ZIP central directory, including entries with data descriptors.
export function readZipEntries(archive) {
    const end = archive.lastIndexOf(Buffer.from([0x50, 0x4b, 0x05, 0x06]));
    assert.ok(end >= 0, 'Missing ZIP end record.');
    const count = archive.readUInt16LE(end + 10);
    let position = archive.readUInt32LE(end + 16);
    const entries = new Map();
    for (let index = 0; index < count; index++) {
        assert.equal(archive.readUInt32LE(position), 0x02014b50, 'Invalid ZIP directory entry.');
        const method = archive.readUInt16LE(position + 10);
        const compressedSize = archive.readUInt32LE(position + 20);
        const size = archive.readUInt32LE(position + 24);
        const nameLength = archive.readUInt16LE(position + 28);
        const extraLength = archive.readUInt16LE(position + 30);
        const commentLength = archive.readUInt16LE(position + 32);
        const local = archive.readUInt32LE(position + 42);
        const name = archive.toString('utf8', position + 46, position + 46 + nameLength);
        assert.equal(archive.readUInt32LE(local), 0x04034b50, 'Invalid ZIP file entry.');
        assert.ok(!entries.has(name), `Duplicate ZIP entry: ${name}`);
        const start = local + 30 + archive.readUInt16LE(local + 26) + archive.readUInt16LE(local + 28);
        const compressed = archive.subarray(start, start + compressedSize);
        assert.ok(method === 0 || method === 8, `Unsupported ZIP method: ${method}`);
        const bytes = method === 8 ? inflateRawSync(compressed) : compressed;
        assert.equal(bytes.length, size, `Incorrect ZIP entry length: ${name}`);
        entries.set(name, bytes);
        position += 46 + nameLength + extraLength + commentLength;
    }
    return entries;
}
