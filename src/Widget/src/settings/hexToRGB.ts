// CORSAIR ColorTools helper, adapted from references/common-tools.md.
export function hexToRGB(hex: string): string {
    const value = parseInt(hex.replace('#', ''), 16);

    return `${(value >> 16) & 255}, ${(value >> 8) & 255}, ${value & 255}`;
}