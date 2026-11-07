import type { MediaConfig } from './MediaConfig.ts';

export function normalizeMediaConfig(raw: unknown): MediaConfig | null {
    if (!raw) {
        return null;
    }

    const value = typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
    const path = typeof raw === 'string' ? raw : (value.pathToAsset ?? value.path ?? value.value);

    if (typeof path !== 'string' || !path) {
        return null;
    }

    // Media is selected locally through iCUE, never fetched from an external host.
    if (!/^(file:|qrc:|resource:|data:image\/|[a-z]:[\\/])/i.test(path)) {
        return null;
    }

    if (!/^data:image\//i.test(path) && !/\.(png|jpe?g|gif|webp|svg|bmp)(?:[?#]|$)/i.test(path)) {
        return null;
    }

    const number = (field: unknown, fallback = 0) => (Number.isFinite(Number(field)) ? Number(field) : fallback);
    const mediaPath = /^[a-z]:[\\/]/i.test(path)
        ? 'file:///' + path.slice(0, 2) + path.slice(2).replaceAll('\\', '/').split('/').map(encodeURIComponent).join('/')
        : path;

    return {
        path: mediaPath,
        baseWidth: number(value.baseWidth ?? value.baseSizeX),
        baseHeight: number(value.baseHeight ?? value.baseSizeY),
        scale: Math.max(0.01, Math.min(100, number(value.scale, 1))),
        positionX: number(value.positionX),
        positionY: number(value.positionY),
        angle: number(value.angle)
    };
}