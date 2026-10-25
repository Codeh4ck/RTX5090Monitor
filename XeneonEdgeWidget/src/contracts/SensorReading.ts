import type { SensorIdentity } from './SensorIdentity.ts';

export interface SensorReading {
    identity: SensorIdentity;
    unit: string;
    value: number | null;
    observedAt: string;
    quality: 'Valid' | 'Stale' | 'Unavailable';
}