import { WidgetApplication } from './WidgetApplication.ts';

let application: WidgetApplication | null = null;

export function initialize(read: (name: string) => unknown, temperatureUnit = '°C', instance = 'default', openRelease?: (url: string) => boolean, updatesReady = true): void {
    application ??= new WidgetApplication(document.getElementById('rtx-monitor')!, instance, openRelease);
    application.setUpdateIdentity(instance);
    application.updateSettings(read, temperatureUnit, updatesReady);
}