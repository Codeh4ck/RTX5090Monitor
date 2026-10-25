import type { WarningState } from './WarningState.ts';

export interface WarningSnapshot {
    instanceId: string;
    sequence: number;
    notificationsEnabled: boolean;
    states: WarningState[];
}