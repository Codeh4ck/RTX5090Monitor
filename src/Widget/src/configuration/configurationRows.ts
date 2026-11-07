import type { ConfigurationRow } from './ConfigurationRow.ts';

const labels: Record<string, string> = {
    pollIntervalMs: 'Polling interval (ms)',
    staleAfterMs: 'Readings expire after (ms)',
    streamIntervalMs: 'Sensor stream interval (ms)',
    deviceId: 'Selected device',
    devicePath: 'Selected device',
    portName: 'Serial port',
    imbalanceAmperageThreshold: 'Imbalance activation (A)',
    imbalancePercentageThreshold: 'Imbalance activation (%)',
    imbalanceDurationThresholdMs: 'Imbalance duration (ms)',
    wireOvercurrentAmperageThreshold: 'Per-pin overcurrent (A)',
    wireOvercurrentThresholdMs: 'Overcurrent duration (ms)',
    fanControl: 'Fan control',
    fanDutyPercent: 'Requested fan duty (%)',
    wireOcpFaultThresholdA: 'Per-pin overcurrent (A)',
    friendlyName: 'Device name'
};

export function configurationRows(value: Record<string, unknown>, prefix = ''): ConfigurationRow[] {
    const rows: ConfigurationRow[] = [];

    for (const [key, entry] of Object.entries(value)) {
        const name = labels[key] ?? key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (letter) => letter.toUpperCase());
        const label = prefix ? `${prefix} / ${name}` : name;

        if (Array.isArray(entry)) {
            if (!entry.length) {
                rows.push({ label, value: 'None' });
            }

            entry.forEach((item, index) => {
                if (item !== null && typeof item === 'object') {
                    rows.push(...configurationRows(item, `${label} ${index + 1}`));
                } else {
                    rows.push({ label: `${label} ${index + 1}`, value: String(item ?? '-') });
                }
            });
        } else if (entry !== null && typeof entry === 'object') {
            rows.push(...configurationRows(entry as Record<string, unknown>, key === 'options' ? prefix : label));
        } else {
            const automatic = ['deviceId', 'devicePath', 'portName'].includes(key);
            rows.push({
                label,
                value:
                    entry === null || entry === ''
                        ? automatic
                            ? 'Automatic'
                            : '-'
                        : typeof entry === 'boolean'
                          ? entry
                              ? 'Enabled'
                              : 'Disabled'
                          : typeof entry === 'number'
                            ? String(Number(entry.toFixed(2)))
                            : String(entry)
            });
        }
    }

    return rows;
}