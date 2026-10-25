export class PeakStore {
    private values = new Map<string, number>();

    observe(key: string, value: number | null): number | null {
        if (value === null) {
            return null;
        }

        const peak = Math.max(this.values.get(key) ?? value, value);
        this.values.set(key, peak);

        if (this.values.size > 256) {
            this.values.delete(this.values.keys().next().value!);
        }

        return peak;
    }

    clear(): void {
        this.values.clear();
    }
}