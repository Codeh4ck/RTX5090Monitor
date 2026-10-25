import { WidgetApplication } from './WidgetApplication.ts';

let application: WidgetApplication | null = null;

export function initialize(read: (name: string) => unknown, temperatureUnit = '°C'): void {
    application ??= new WidgetApplication(document.getElementById('rtx-monitor')!);
    application.updateSettings(read, temperatureUnit);
}