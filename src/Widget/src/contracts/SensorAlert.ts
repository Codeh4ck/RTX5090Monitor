import type { AlertPin } from './AlertPin.ts';

export interface SensorAlert {
    pluginId: string;
    deviceId: string;
    kind: string;
    reportedAt: string;
    pins: AlertPin[];
    percentageDifference: number | null;
}