import type { WarningSnapshot } from '../contracts/WarningSnapshot.ts';

export class WarningTracker {
    private readonly seen = new Map<string, number>();
    private instance = '';
    private enabled = true;

    update(feed: WarningSnapshot | null | undefined, enabled = true): boolean {
        if (!enabled) {
            this.enabled = false;

            return false;
        }

        if (!feed?.notificationsEnabled) {
            return false;
        }

        if (!this.enabled || this.instance !== feed.instanceId) {
            this.seen.clear();
        }

        this.enabled = true;
        this.instance = feed.instanceId;

        const keys = new Set(feed.states.map((state) => `${state.id}/${state.episodeId}`));

        for (const key of this.seen.keys()) {
            if (!keys.has(key)) {
                this.seen.delete(key);
            }
        }

        let due = false;

        for (const state of feed.states) {
            if (
                state.phase !== 'Active' ||
                state.notificationSequence <= 0 ||
                (!state.policy.notifyOnStart && !state.policy.repeatWhileActive)
            ) 
            {
                continue;
            }

            const key = `${state.id}/${state.episodeId}`;

            if (state.notificationSequence > (this.seen.get(key) ?? 0)) {
                this.seen.set(key, state.notificationSequence);
                due = true;
            }
        }

        return due;
    }
}