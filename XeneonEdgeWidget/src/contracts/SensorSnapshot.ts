import type { WarningSnapshot } from './WarningSnapshot.ts';
import type { PluginHealth } from './PluginHealth.ts';
import type { ResolvedSelection } from './ResolvedSelection.ts';
import type { SensorAlert } from './SensorAlert.ts';
import type { SensorReading } from './SensorReading.ts';

export interface SensorSnapshot {
    schemaVersion: 1;
    sequence: number;
    timestamp: string;
    sensors: SensorReading[];
    plugins: PluginHealth[];
    selections: ResolvedSelection[];
    configurationRevision: number;
    alerts: SensorAlert[];
    warnings?: WarningSnapshot | null;
}