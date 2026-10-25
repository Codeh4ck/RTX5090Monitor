declare const RTX_WIDGET_VERSION: string;

export function widgetVersion(): string {
    return typeof RTX_WIDGET_VERSION === 'string' ? RTX_WIDGET_VERSION : 'unknown';
}
