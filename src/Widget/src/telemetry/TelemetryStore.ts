import type { ConfigurationSnapshot } from '../contracts/ConfigurationSnapshot.ts';
import type { ConnectionState } from '../contracts/ConnectionState.ts';
import type { PluginInfo } from '../contracts/PluginInfo.ts';
import type { SensorReading } from '../contracts/SensorReading.ts';
import type { SensorSnapshot } from '../contracts/SensorSnapshot.ts';
import { sensorKey } from './sensorKey.ts';

export class TelemetryStore {
    snapshot: SensorSnapshot | null = null;
    catalog: PluginInfo[] = [];
    configuration: ConfigurationSnapshot | null = null;
    connection: ConnectionState = 'connecting';
    connectionMessage = 'Connecting to the monitoring service';
    wireLimit: number | null = null;
    wireLimitDevice: string | null = null;
    wireName = '';
    msiName = 'MSI MPG Ai1300TS / Ai1600TS';
    private readonly indexed = new Map<string, SensorReading>();
    private readonly observed = new Map<string, number>();

    update(snapshot: SensorSnapshot): void {
        this.snapshot = snapshot;
        this.indexed.clear();
        this.observed.clear();

        for (const reading of snapshot.sensors) {
            this.indexed.set(reading.identity.key, reading);
            this.observed.set(reading.identity.key, Date.parse(reading.observedAt));
        }
    }

    clear(): void {
        this.snapshot = null;
        this.catalog = [];
        this.configuration = null;
        this.wireLimit = null;
        this.wireLimitDevice = null;
        this.wireName = '';
        this.msiName = 'MSI MPG Ai1300TS / Ai1600TS';
        this.indexed.clear();
        this.observed.clear();
    }

    staleAfter(pluginId: string): number {
        const value = this.configuration?.configuration.plugins[pluginId]?.staleAfterMs;

        return typeof value === 'number' && value >= 100 && value <= 300000 ? value : 5000;
    }

    valid(reading: SensorReading | undefined, now: number): reading is SensorReading & { value: number;} {
        if (!reading || reading.quality !== 'Valid' || reading.value === null || this.connection === 'incompatible') {
            return false;
        }

        const state = this.snapshot?.plugins.find((plugin) => plugin.pluginId === reading.identity.pluginId)?.state;
        const enabled =
            this.configuration?.configuration.plugins[reading.identity.pluginId]?.enabled ??
            this.catalog.find((plugin) => plugin.id === reading.identity.pluginId)?.enabled;

        const age = now - (this.observed.get(reading.identity.key) ?? NaN);

        return (
            enabled !== false &&
            state === 'Ready' &&
            Number.isFinite(age) &&
            age >= -30000 &&
            age <= this.staleAfter(reading.identity.pluginId)
        );
    }

    device(pluginId: string, requested: string, now: number): string | null {
        if (requested.trim()) {
            return requested.trim();
        }

        return (
            this.snapshot?.sensors.find((reading) => reading.identity.pluginId === pluginId && this.valid(reading, now))?.identity
                .deviceId ?? null
        );
    }

    reading(
        pluginId: string,
        deviceId: string | null,
        component: string,
        metric: string,
        channel: number | null = null,
        connectorId: string | null = null
    ): SensorReading | undefined {
        return deviceId === null
            ? undefined
            : this.indexed.get(
                  sensorKey({
                      pluginId,
                      deviceId,
                      component,
                      metric,
                      channel,
                      connectorId
                  })
              );
    }

    value(
        pluginId: string,
        deviceId: string | null,
        component: string,
        metric: string,
        now: number,
        channel: number | null = null,
        connectorId: string | null = null
    ): number | null {
        const reading = this.reading(pluginId, deviceId, component, metric, channel, connectorId);

        return this.valid(reading, now) ? reading.value : null;
    }

    byKey(key: string, now: number): SensorReading | null {
        const reading = this.indexed.get(key);

        return this.valid(reading, now) ? reading : null;
    }

    reporting(pluginId: string, now: number): boolean {
        return this.snapshot?.sensors.some((reading) => reading.identity.pluginId === pluginId && this.valid(reading, now)) ?? false;
    }

    state(pluginId: string, now: number): string {
        const info = this.catalog.find((plugin) => plugin.id === pluginId);
        const enabled = this.configuration?.configuration.plugins[pluginId]?.enabled ?? info?.enabled;

        if (enabled === false) {
            return 'Disabled';
        }

        if (!this.snapshot && !info) {
            return this.connection === 'connecting' ? 'Connecting' : 'Offline';
        }

        if (this.reporting(pluginId, now)) {
            return this.connection === 'live' ? 'Ready' : 'Reconnecting';
        }

        const health = this.snapshot?.plugins.find((plugin) => plugin.pluginId === pluginId);
        const state = health?.state ?? info?.state ?? 'NotInstalled';

        return state === 'Ready' ? 'Stale' : state;
    }

    limit(pluginId: string, deviceId: string | null): number | null {
        if (pluginId === 'wireview-pro2') {
            return deviceId === this.wireLimitDevice ? this.wireLimit : null;
        }

        const value = this.configuration?.configuration.plugins['asus-astral']?.options?.wireOvercurrentAmperageThreshold;

        return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : null;
    }
}