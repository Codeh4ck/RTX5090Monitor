import type { WarningPolicy } from './WarningPolicy.ts';

export interface WarningState {
    id: string;
    pluginId: string;
    deviceId: string;
    typeId: string;
    icon: string;
    title: string;
    detail: string;
    phase: 'Active' | 'Recovering' | 'Unavailable';
    episodeId: string;
    startedAt: string;
    policy: WarningPolicy;
    notificationSequence: number;
    lastNotificationAt: string | null;
}