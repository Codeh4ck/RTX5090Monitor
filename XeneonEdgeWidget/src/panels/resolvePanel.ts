import { Settings } from '../settings/Settings.ts';
import { TelemetryStore } from '../telemetry/TelemetryStore.ts';
import type { PanelBinding } from './PanelBinding.ts';

export function resolvePanel(slot: number, settings: Settings, store: TelemetryStore, now: number): PanelBinding {
    const content = settings.text(`panel${slot}`);
    const automatic = content.startsWith('auto');
    const available = (id: string) => {
        if (content !== 'autoPins') {
            return store.reporting(id, now);
        }

        return store.snapshot?.sensors.some(
            (reading) =>
                reading.identity.pluginId === id &&
                ['GpuPin', 'PsuPin'].includes(reading.identity.component) &&
                ['Current', 'Voltage', 'Power'].includes(reading.identity.metric) &&
                store.valid(reading, now)
        );
    };

    const pluginId = automatic
        ? (['asus-astral', 'wireview-pro2', 'msi-psu'].find(available) ?? 'asus-astral')
        : content.startsWith('wireview')
          ? 'wireview-pro2'
          : content.startsWith('msi')
            ? 'msi-psu'
            : 'asus-astral';

    return {
        kind: content === 'off' ? 'off' : /Status$/.test(content) ? 'status' : 'pins',
        pluginId,
        deviceId: store.device(pluginId, '', now),
        connectorId: pluginId === 'msi-psu' ? (content === 'msiConnector2' ? '2' : '1') : null,
        automatic
    };
}