export interface SafeguardExecutionNotice {
    id: string;
    script: string;
    connector: 1 | 2;
    startedAt: string;
    status: string;
}