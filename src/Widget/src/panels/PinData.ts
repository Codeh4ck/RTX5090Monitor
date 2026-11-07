export interface PinData {
    value: string;
    number: number | null;
    current: number | null;
    headroom: string;
    headroomValue: number | null;
    comparison: string;
    fill: number;
    peak: number | null;
    warning: boolean;
    overflow: boolean;
}