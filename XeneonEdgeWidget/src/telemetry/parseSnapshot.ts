import type { SensorSnapshot } from '../contracts/SensorSnapshot.ts';
import { sensorKey } from './sensorKey.ts';

const units: Record<string, string> = {
    Power: 'Watts',
    Current: 'Amperes',
    Voltage: 'Volts',
    Temperature: 'Celsius',
    Clock: 'Megahertz',
    FanSpeed: 'Rpm',
    Utilization: 'Percent',
    MemoryUsed: 'Megabytes',
    MemoryTotal: 'Megabytes',
    MemoryFree: 'Megabytes',
    PowerCapability: 'Watts',
    FaultStatus: 'Bitmask',
    FaultLog: 'Bitmask',
    ImbalancePercentage: 'Percent',
    ImbalanceDetected: 'Boolean',
    OvercurrentDetected: 'Boolean',
    FanDuty: 'Percent',
    FanAutomatic: 'Boolean'
};

function isRecord(value: unknown): value is Record<string, unknown> {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
}

export function parseSnapshot(value: unknown): SensorSnapshot {
    if (!isRecord(value) || value.schemaVersion !== 1) {
        throw new Error('Unsupported service schema. Update the service or widget.');
    }

    if (
        !Number.isSafeInteger(value.sequence) ||
        !Number.isSafeInteger(value.configurationRevision) ||
        typeof value.timestamp !== 'string' ||
        !Number.isFinite(Date.parse(value.timestamp)) ||
        !Array.isArray(value.sensors) ||
        value.sensors.length > 4096 ||
        !Array.isArray(value.plugins) ||
        !Array.isArray(value.selections) ||
        !Array.isArray(value.alerts)
    ) {
        throw new Error('Invalid service snapshot.');
    }

    const keys = new Set<string>();

    for (const reading of value.sensors) {
        if (!isRecord(reading) || !isRecord(reading.identity)) {
            throw new Error('Invalid sensor identity.');
        }

        const identity = reading.identity;

        if (
            typeof identity.pluginId !== 'string' ||
            !identity.pluginId ||
            typeof identity.deviceId !== 'string' ||
            !identity.deviceId ||
            typeof identity.component !== 'string' ||
            typeof identity.metric !== 'string' ||
            !(identity.channel == null || Number.isSafeInteger(identity.channel)) ||
            !(identity.connectorId == null || typeof identity.connectorId === 'string') ||
            (reading.value !== null && (typeof reading.value !== 'number' || !Number.isFinite(reading.value))) ||
            typeof reading.observedAt !== 'string' ||
            !Number.isFinite(Date.parse(reading.observedAt)) ||
            !['Valid', 'Stale', 'Unavailable'].includes(String(reading.quality)) ||
            typeof reading.unit !== 'string' ||
            (units[identity.metric] && units[identity.metric] !== reading.unit)
        ) {
            throw new Error('Invalid sensor reading.');
        }

        const typed = identity as unknown as SensorSnapshot['sensors'][number]['identity'];
        typed.channel ??= null;
        typed.connectorId ??= null;
        typed.key = sensorKey(typed);

        if (keys.has(typed.key)) {
            throw new Error('Duplicate sensor identity.');
        }

        keys.add(typed.key);
    }

    if (
        value.plugins.some((plugin) => !isRecord(plugin) || typeof plugin.pluginId !== 'string' || typeof plugin.state !== 'string') ||
        value.selections.some(
            (selection) =>
                !isRecord(selection) ||
                typeof selection.name !== 'string' ||
                !['Selected', 'Fallback', 'Unavailable'].includes(String(selection.state)) ||
                !(selection.sensorKey === null || typeof selection.sensorKey === 'string')
        ) ||
        value.alerts.some(
            (alert) =>
                !isRecord(alert) ||
                typeof alert.pluginId !== 'string' ||
                typeof alert.deviceId !== 'string' ||
                typeof alert.kind !== 'string' ||
                !alert.kind.trim() ||
                !Array.isArray(alert.pins) ||
                typeof alert.reportedAt !== 'string' ||
                !Number.isFinite(Date.parse(alert.reportedAt)) ||
                alert.pins.some(
                    (pin) =>
                        !isRecord(pin) ||
                        !Number.isInteger(pin.channel) ||
                        typeof pin.currentA !== 'number' ||
                        !Number.isFinite(pin.currentA)
                )
        )
    ) {
        throw new Error('Invalid service health, selection or alert.');
    }

    if (value.warnings != null) {
        const feed = value.warnings;

        if (
            !isRecord(feed) ||
            typeof feed.instanceId !== 'string' ||
            !feed.instanceId ||
            !Number.isSafeInteger(feed.sequence) ||
            (feed.sequence as number) < 0 ||
            typeof feed.notificationsEnabled !== 'boolean' ||
            !Array.isArray(feed.states) ||
            feed.states.length > 256 ||
            feed.states.some(
                (state) =>
                    !isRecord(state) ||
                    typeof state.id !== 'string' ||
                    !state.id.trim() ||
                    typeof state.episodeId !== 'string' ||
                    !state.episodeId.trim() ||
                    typeof state.pluginId !== 'string' ||
                    !state.pluginId.trim() ||
                    typeof state.deviceId !== 'string' ||
                    !state.deviceId.trim() ||
                    typeof state.typeId !== 'string' ||
                    !state.typeId.trim() ||
                    typeof state.icon !== 'string' ||
                    typeof state.title !== 'string' ||
                    typeof state.detail !== 'string' ||
                    !['Active', 'Recovering', 'Unavailable'].includes(String(state.phase)) ||
                    !Number.isSafeInteger(state.notificationSequence) ||
                    (state.notificationSequence as number) < 0 ||
                    (state.notificationSequence as number) > (feed.sequence as number) ||
                    typeof state.startedAt !== 'string' ||
                    !Number.isFinite(Date.parse(state.startedAt)) ||
                    !isRecord(state.policy) ||
                    typeof state.policy.notifyOnStart !== 'boolean' ||
                    typeof state.policy.repeatWhileActive !== 'boolean' ||
                    !Number.isInteger(state.policy.repeatIntervalSeconds) ||
                    (state.policy.repeatIntervalSeconds as number) < 5 ||
                    (state.policy.repeatIntervalSeconds as number) > 3600 ||
                    !Number.isInteger(state.policy.clearAfterSeconds) ||
                    (state.policy.clearAfterSeconds as number) < 0 ||
                    (state.policy.clearAfterSeconds as number) > 60
            )
        ) {
            throw new Error('Invalid service warning state.');
        }

        if (new Set(feed.states.map((state) => (state as Record<string, unknown>).id)).size !== feed.states.length) {
            throw new Error('Duplicate service warning identity.');
        }
    }

    return value as unknown as SensorSnapshot;
}