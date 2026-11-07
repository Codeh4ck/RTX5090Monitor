export interface SensorIdentity {
    pluginId: string;
    deviceId: string;
    component: string;
    metric: string;
    channel: number | null;
    connectorId: string | null;
    key: string;
}