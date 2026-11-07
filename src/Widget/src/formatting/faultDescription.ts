export function faultDescription(value: number | null): string {
    if (value === null) {
        return '-';
    }

    if (value === 0) {
        return 'Clear';
    }

    const names: string[] = [];

    for (const [mask, name] of [
        [2, 'Overtemperature'],
        [4, 'Overcurrent'],
        [8, 'Wire overcurrent'],
        [16, 'Overpower'],
        [32, 'Current imbalance']
    ] as const) {
        if ((value & mask) !== 0) {
            names.push(name);
        }
    }

    const unknown = value & ~0x3e;

    if (unknown) {
        names.push(`Other flags 0x${unknown.toString(16).toUpperCase().padStart(4, '0')}`);
    }

    return names.join(', ');
}