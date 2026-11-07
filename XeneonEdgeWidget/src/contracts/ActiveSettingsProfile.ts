export interface ActiveSettingsProfile {
    gpuDeviceId: string;
    wireViewDeviceId: string | null;
    profile: {
        name: string;
        gpuModel: string;
        currentCeiling: number;
        powerCeilingPerPin: number | null;
        powerFallbackPerPin: number;
        voltageMinimum: number;
        voltageMaximum: number;
        useWireViewOvercurrentFallback: boolean;
        overcurrentAmperes: number;
    };
}
