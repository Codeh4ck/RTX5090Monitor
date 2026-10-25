import type { WarningDefinition } from './WarningDefinition.ts';

export interface PluginInfo {
    id: string;
    name: string;
    enabled: boolean;
    state: string;
    lastSuccessAt: string | null;
    message: string | null;
    capabilities: string[];
    warnings?: WarningDefinition[];
}