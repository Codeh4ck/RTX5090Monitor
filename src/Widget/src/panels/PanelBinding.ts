export interface PanelBinding {
    kind: 'pins' | 'status' | 'off';
    pluginId: string;
    deviceId: string | null;
    connectorId: string | null;
    automatic: boolean;
}