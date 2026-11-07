import type { PluginSettings } from './PluginSettings.ts';

export interface ConfigurationSnapshot {
    revision: number;
    configuration: {
        plugins: Record<string, PluginSettings>;
        monitoring?: Record<string, unknown>;
    };
}