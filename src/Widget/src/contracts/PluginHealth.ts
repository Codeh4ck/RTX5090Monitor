export interface PluginHealth {
    pluginId: string;
    state: string;
    lastSuccessAt: string | null;
    message: string | null;
}