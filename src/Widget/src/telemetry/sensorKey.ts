import type { SensorIdentity } from '../contracts/SensorIdentity.ts';

function escapeSegment(value: string): string {
    return encodeURIComponent(value).replace(/[!'()*]/g, (character) => `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
}

export function sensorKey(identity: Omit<SensorIdentity, 'key'>): string {
    const component = identity.component.charAt(0).toLowerCase() + identity.component.slice(1);
    const metric = identity.metric.charAt(0).toLowerCase() + identity.metric.slice(1);

    return [
        escapeSegment(identity.pluginId),
        escapeSegment(identity.deviceId),
        component,
        escapeSegment(identity.connectorId ?? 'default'),
        identity.channel?.toString() ?? 'all',
        metric
    ].join('/');
}