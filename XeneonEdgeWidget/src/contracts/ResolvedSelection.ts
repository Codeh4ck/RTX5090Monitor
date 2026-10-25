export interface ResolvedSelection {
    name: string;
    state: 'Selected' | 'Fallback' | 'Unavailable';
    sensorKey: string | null;
}