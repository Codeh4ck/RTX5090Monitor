export function fixture(now = Date.now()) {
    const observedAt = new Date(now).toISOString();
    const sensors = [];
    const add = (pluginId, component, metric, unit, value, channel = null, connectorId = null) => sensors.push({
        identity: { pluginId, deviceId: pluginId === 'msi-psu' ? 'hid#vid_0db0&pid_808c#fixture' : pluginId === 'asus-astral' ? '/gpu-nvidia/0' : 'com3', component, metric, channel, connectorId },
        unit, value, observedAt, quality: 'Valid'
    });
    for (const plugin of ['asus-astral', 'wireview-pro2']) {
        add(plugin, 'GpuConnector', 'Power', 'Watts', 432, 1);
        add(plugin, 'GpuConnector', 'Current', 'Amperes', 36, 1);
        if (plugin === 'wireview-pro2') add(plugin, 'GpuConnector', 'PowerCapability', 'Watts', 600, 1);
        [5.82, 6.18, 6.08, 5.92, 5.98, 6.02].forEach((value, index) => {
            add(plugin, 'GpuPin', 'Current', 'Amperes', value, index + 1);
            add(plugin, 'GpuPin', 'Voltage', 'Volts', 12 + (index % 2) * .01, index + 1);
            add(plugin, 'GpuPin', 'Power', 'Watts', value * 12, index + 1);
        });
    }
    add('asus-astral', 'GpuCore', 'Clock', 'Megahertz', 2760);
    add('asus-astral', 'GpuMemory', 'Clock', 'Megahertz', 14001);
    add('asus-astral', 'GpuCore', 'Temperature', 'Celsius', 62);
    add('asus-astral', 'GpuMemory', 'Temperature', 'Celsius', 70);
    add('asus-astral', 'GpuCore', 'Utilization', 'Percent', 97);
    add('asus-astral', 'GpuMemory', 'Utilization', 'Percent', 75);
    add('asus-astral', 'GpuMemory', 'MemoryUsed', 'Megabytes', 24576);
    add('asus-astral', 'GpuMemory', 'MemoryTotal', 'Megabytes', 32768);
    add('asus-astral', 'GpuBoard', 'Power', 'Watts', 470);
    add('asus-astral', 'GpuFan', 'FanSpeed', 'Rpm', 1220, 1);
    add('asus-astral', 'GpuFan', 'FanSpeed', 'Rpm', 1185, 2);
    add('wireview-pro2', 'MonitorBoard', 'Temperature', 'Celsius', 38, 1);
    add('wireview-pro2', 'MonitorBoard', 'Temperature', 'Celsius', 39, 2);
    add('wireview-pro2', 'ExternalProbe', 'Temperature', 'Celsius', 34, 1);
    add('wireview-pro2', 'ExternalProbe', 'Temperature', 'Celsius', 35, 2);
    add('wireview-pro2', 'MonitorBoard', 'FaultStatus', 'Bitmask', 0);
    add('wireview-pro2', 'MonitorBoard', 'FaultLog', 'Bitmask', 0);
    for (const connector of ['1', '2']) {
        const values = connector === '1' ? [5.79, 6.16, 6.09, 5.91, 5.99, 6] : [0, 0, 0, 0, 0, 0];
        values.forEach((value, index) => add('msi-psu', 'PsuPin', 'Current', 'Amperes', value, index + 1, connector));
        add('msi-psu', 'PsuConnector', 'Current', 'Amperes', values.reduce((a, b) => a + b, 0), null, connector);
    }
    add('msi-psu', 'Psu', 'Temperature', 'Celsius', 41);
    add('msi-psu', 'Psu', 'FanSpeed', 'Rpm', 710, 1);
    add('msi-psu', 'Psu', 'FanDuty', 'Percent', 0, 1);
    add('msi-psu', 'Psu', 'FanAutomatic', 'Boolean', 1, 1);
    return { schemaVersion: 1, sequence: 1, timestamp: observedAt, sensors,
        plugins: ['asus-astral', 'wireview-pro2', 'msi-psu'].map(pluginId => ({ pluginId, state: 'Ready', lastSuccessAt: observedAt, message: null })),
        selections: [], alerts: [], warnings: { instanceId: "fixture", sequence: 0, notificationsEnabled: true, states: [] }, configurationRevision: 7 };
}

export function configuration(staleAfterMs = 5000) {
    return { revision: 7, configuration: { monitoring: { streamIntervalMs: 1000, selections: [] }, plugins: Object.fromEntries(['asus-astral', 'wireview-pro2', 'msi-psu'].map(id => [id, {
        enabled: true, pollIntervalMs: 1000, staleAfterMs, options: id === 'asus-astral' ? { wireOvercurrentAmperageThreshold: 9.2 } : {}
    }])) } };
}

export function catalog() {
    return { schemaVersion: 1, plugins: ['asus-astral', 'wireview-pro2', 'msi-psu'].map(id => ({ id, name: id, enabled: true,
        state: 'Ready', lastSuccessAt: new Date().toISOString(), message: null, capabilities: [] })) };
}
