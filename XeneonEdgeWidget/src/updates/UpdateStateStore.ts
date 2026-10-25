import type { UpdateCheckState } from './UpdateCheckState.ts';
import { parseReleaseManifest, parseVersion } from './versions.ts';

export class UpdateStateStore {
    private readonly key: string;
    private readonly storage: Pick<Storage, 'getItem' | 'setItem'> | null;

    constructor(key: string, storage: Pick<Storage, 'getItem' | 'setItem'> | null) {
        this.key = key;
        this.storage = storage;
    }

    load(): UpdateCheckState {
        try {
            const value = JSON.parse(this.storage?.getItem(this.key) ?? 'null') as Partial<UpdateCheckState> | null;
            const dismissal = value?.dismissal;

            return {
                lastSuccessfulCheck: typeof value?.lastSuccessfulCheck === 'number' && Number.isFinite(value.lastSuccessfulCheck)
                    ? value.lastSuccessfulCheck : null,
                manifest: value?.manifest ? parseReleaseManifest(value.manifest) : null,
                dismissal: dismissal && parseVersion(dismissal.version) && typeof dismissal.skipped === 'boolean'
                    && (dismissal.remindAfter === null || typeof dismissal.remindAfter === 'number' && Number.isFinite(dismissal.remindAfter))
                    ? dismissal : null
            };
        } catch {
            return { lastSuccessfulCheck: null, manifest: null, dismissal: null };
        }
    }

    save(state: UpdateCheckState): void {
        try {
            this.storage?.setItem(this.key, JSON.stringify(state));
        } catch {
            // Storage can be unavailable in a restricted web view. Monitoring still works.
        }
    }
}
