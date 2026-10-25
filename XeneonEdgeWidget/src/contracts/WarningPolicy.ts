export interface WarningPolicy {
    notifyOnStart: boolean;
    repeatWhileActive: boolean;
    repeatIntervalSeconds: number;
    clearAfterSeconds: number;
}