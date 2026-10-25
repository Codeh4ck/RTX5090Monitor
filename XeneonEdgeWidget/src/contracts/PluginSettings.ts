export interface PluginSettings {
    enabled: boolean;
    pollIntervalMs: number;
    staleAfterMs: number;
    options: Record<string, unknown> | null;
}