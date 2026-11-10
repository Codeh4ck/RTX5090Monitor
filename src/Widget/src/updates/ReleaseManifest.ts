export interface ReleaseManifest {
    schemaVersion: 1;
    releaseTag: string;
    releaseUrl?: string;
    desktop: string;
    service: string;
    widget: string;
}
