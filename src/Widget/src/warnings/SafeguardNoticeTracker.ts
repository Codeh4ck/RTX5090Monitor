import type { SafeguardExecutionNotice } from '../contracts/SafeguardExecutionNotice.ts';

export class SafeguardNoticeTracker {
    private readonly seen = new Map<string, number>();

    private readonly key: string;
    private readonly storage: Storage | null;

    constructor(key: string, storage: Storage | null) {
        this.key = key;
        this.storage = storage;

        try {
            const saved: unknown = JSON.parse(storage?.getItem(key) ?? '{}');

            if (saved && typeof saved === 'object' && !Array.isArray(saved)) {
                for (const [id, timestamp] of Object.entries(saved)) {
                    if (typeof timestamp === 'number' && Number.isFinite(timestamp)) {
                        this.seen.set(id, timestamp);
                    }
                }
            }
        } catch {
            // The widget can still deduplicate notices during this session.
        }
    }

    receive(executions: readonly SafeguardExecutionNotice[]): SafeguardExecutionNotice[] {
        const pending = executions.filter((execution) => {
            if (this.seen.has(execution.id)) {
                return false;
            }

            this.seen.set(execution.id, Date.parse(execution.startedAt));
            return true;
        });

        if (pending.length) {
            const cutoff = Date.now() - 365 * 24 * 60 * 60 * 1000;

            for (const [id, timestamp] of this.seen) {
                if (timestamp < cutoff) {
                    this.seen.delete(id);
                }
            }

            try {
                this.storage?.setItem(this.key, JSON.stringify(Object.fromEntries(this.seen)));
            } catch {
                // The embedded host can disallow persistent storage.
            }
        }

        return pending;
    }
}