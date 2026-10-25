import type { ReleaseManifest } from './ReleaseManifest.ts';
import type { UpdateDismissal } from './UpdateDismissal.ts';

export interface UpdateCheckState {
    lastSuccessfulCheck: number | null;
    manifest: ReleaseManifest | null;
    dismissal: UpdateDismissal | null;
}
