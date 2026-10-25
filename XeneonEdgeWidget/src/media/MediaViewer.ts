import type { MediaConfig } from './MediaConfig.ts';

// Adapted from the CORSAIR MediaViewer included in references/media-backgrounds.md.
export class MediaViewer {
    private container: HTMLElement;
    private config: MediaConfig | null = null;
    private media: HTMLImageElement | null = null;
    private position: HTMLElement | null = null;
    private scale: HTMLElement | null = null;
    private rotation: HTMLElement | null = null;
    private onFailure: (message: string) => void;

    constructor(container: HTMLElement, onFailure: (message: string) => void) {
        this.container = container;
        this.onFailure = onFailure;
    }

    loadMedia(config: MediaConfig | null): void {
        if (!config) {
            this.clear();

            return;
        }

        if (this.config?.path === config.path) {
            this.config = config;
            this.applyTransform();

            return;
        }

        this.clear();
        this.config = config;
        this.media = document.createElement('img');
        this.media.addEventListener('load', () => this.applyTransform());
        this.media.addEventListener('error', () => {
            this.clear();
            this.onFailure('Background image could not be loaded. Telemetry remains available.');
        });

        this.position = document.createElement('div');
        this.position.className = 'position-container';
        this.scale = document.createElement('div');
        this.scale.className = 'scale-container';
        this.rotation = document.createElement('div');
        this.rotation.className = 'rotation-container';
        this.rotation.append(this.media);
        this.scale.append(this.rotation);
        this.position.append(this.scale);
        this.container.append(this.position);
        this.container.style.visibility = 'visible';
        this.media.src = config.path;
        this.applyTransform();
    }

    applyTransform(): void {
        if (!this.config || !this.position || !this.scale || !this.rotation || !this.media) {
            return;
        }

        const width = this.media.naturalWidth;
        const height = this.media.naturalHeight;
        const factor = Math.min(
            this.container.clientWidth / (this.config.baseWidth || width || 100),
            this.container.clientHeight / (this.config.baseHeight || height || 100)
        );
        this.position.style.transform = `translate(${this.config.positionX * factor}px, ${this.config.positionY * factor}px)`;
        this.scale.style.transform = `scale(${this.config.scale * factor})`;
        this.rotation.style.transform = `rotate(${this.config.angle}deg)`;
    }

    clear(): void {
        this.container.replaceChildren();
        this.container.style.visibility = 'hidden';
        this.config = null;
        this.media = this.position = this.scale = this.rotation = null;
    }
}