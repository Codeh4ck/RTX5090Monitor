import type { WarningSnapshot } from './WarningSnapshot.ts';
import type { PluginHealth } from './PluginHealth.ts';
import type { ResolvedSelection } from './ResolvedSelection.ts';
import type { SensorAlert } from './SensorAlert.ts';
import type { SensorReading } from './SensorReading.ts';

import type { ActiveSettingsProfile } from './ActiveSettingsProfile.ts';
import type { PinWarningLimit } from './PinWarningLimit.ts';

export interface SensorSnapshot {
    schemaVersion: 1;
    sequence: number;
    timestamp: string;
    sensors: SensorReading[];
    plugins: PluginHealth[];
    selections: ResolvedSelection[];
    configurationRevision: number;
    alerts: SensorAlert[];
    settingsProfile?: ActiveSettingsProfile | null;
    pinWarningLimits?: PinWarningLimit[] | null;
    warnings?: WarningSnapshot | null;
}