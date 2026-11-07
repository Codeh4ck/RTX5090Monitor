import type { ConfigurationSnapshot } from '../contracts/ConfigurationSnapshot.ts';
import type { PluginInfo } from '../contracts/PluginInfo.ts';
import { TelemetryStore } from '../telemetry/TelemetryStore.ts';
import { parseSnapshot } from '../telemetry/parseSnapshot.ts';
import { ReadOnlyApi } from './ReadOnlyApi.ts';

export class ServiceClient {
    private store: TelemetryStore;
    private changed: () => void;
    private api: ReadOnlyApi | null = null;
    private socket: WebSocket | null = null;
    private controller = new AbortController();
    private reconnect: ReturnType<typeof setTimeout> | null = null;
    private generation = 0;
    private attempts = 0;
    private revision = -1;
    private metadataAt = 0;
    private metadataBusy = false;
    private limitAt = 0;
    private limitBusy = false;
    private port = 0;

    constructor(store: TelemetryStore, changed: () => void) {
        this.store = store;
        this.changed = changed;
    }

    start(port: number): void {
        if (this.port === port) {
            return;
        }

        this.stop();
        this.port = port;
        this.store.clear();
        this.store.connection = 'connecting';
        this.store.connectionMessage = 'Connecting to the monitoring service';
        this.api = new ReadOnlyApi(port);
        this.controller = new AbortController();
        this.attempts = 0;
        this.revision = -1;
        this.metadataAt = this.limitAt = 0;
        this.metadataBusy = this.limitBusy = false;
        this.connect(this.generation);
        this.changed();
    }

    stop(): void {
        this.generation++;
        this.controller.abort();

        if (this.reconnect !== null) {
            clearTimeout(this.reconnect);
        }

        this.reconnect = null;

        if (this.socket) {
            this.socket.onclose = this.socket.onmessage = this.socket.onerror = this.socket.onopen = null;
            this.socket.close();
        }

        this.socket = null;
        this.port = 0;
    }

    private connect(generation: number): void {
        if (generation !== this.generation || !this.api) {
            return;
        }

        let socket: WebSocket;

        try {
            socket = new WebSocket(this.api.socketUrl());
        } catch {
            this.disconnected(generation);

            return;
        }

        this.socket = socket;

        let lastSequence = -1;
        socket.onopen = () => {
            if (generation !== this.generation) {
                return;
            }

            this.metadataAt = 0;
            void this.refreshMetadata(generation, true);
        };

        socket.onmessage = (event) => {
            if (generation !== this.generation || socket !== this.socket) {
                return;
            }

            try {
                if (typeof event.data !== 'string' || event.data.length > 2000000) {
                    throw new Error('Invalid sensor frame.');
                }

                const snapshot = parseSnapshot(JSON.parse(event.data));

                if (snapshot.sequence <= lastSequence) {
                    return;
                }

                lastSequence = snapshot.sequence;
                this.attempts = 0;

                const revisionChanged = snapshot.configurationRevision !== this.revision;
                this.store.update(snapshot);
                this.store.connection = 'live';
                this.store.connectionMessage = 'Service connected';
                void this.refreshMetadata(generation, revisionChanged);
                void this.refreshWireLimit(generation, revisionChanged);
                this.changed();
            } catch (error) {
                this.store.connection = 'incompatible';
                this.store.connectionMessage = error instanceof Error ? error.message : 'Invalid service data';
                this.changed();
                socket.close(4003, 'Invalid or unsupported sensor frame');
            }
        };

        socket.onerror = () => socket.close();
        socket.onclose = () => {
            if (socket === this.socket) {
                this.disconnected(generation);
            }
        };

        // Discovery does not gate the socket: a transient metadata error must not stop fresh telemetry.
        void this.refreshMetadata(generation, true);
    }

    private disconnected(generation: number): void {
        if (generation !== this.generation) {
            return;
        }

        this.socket = null;

        if (this.store.connection !== 'incompatible') {
            this.store.connection = this.store.snapshot ? 'reconnecting' : 'offline';
            this.store.connectionMessage = 'Service unavailable. Check that RTX Monitor service is running.';
        }

        this.changed();

        const delay = Math.min(15000, 500 * 2 ** Math.min(this.attempts++, 5));
        this.reconnect = setTimeout(() => this.connect(generation), delay);
    }

    private async refreshMetadata(generation: number, force: boolean): Promise<void> {
        if (!this.api || this.metadataBusy || (!force && Date.now() - this.metadataAt < 30000)) {
            return;
        }

        this.metadataBusy = true;

        const api = this.api;
        const signal = this.controller.signal;

        try {
            const results = await Promise.allSettled([
                api.get('/health', signal),
                api.get('/plugins', signal),
                api.get('/configuration', signal)
            ]);

            if (generation !== this.generation) {
                return;
            }

            const catalog = results[1];

            if (catalog?.status === 'fulfilled' && catalog.value && typeof catalog.value === 'object') {
                const value = catalog.value as {
                    schemaVersion?: number;
                    plugins?: PluginInfo[];
                };

                if (
                    value.schemaVersion === 1 &&
                    Array.isArray(value.plugins) &&
                    value.plugins.every(
                        (plugin) =>
                            typeof plugin.id === 'string' &&
                            typeof plugin.name === 'string' &&
                            typeof plugin.enabled === 'boolean' &&
                            typeof plugin.state === 'string'
                    )
                ) {
                    this.store.catalog = value.plugins;
                }
            }

            const configuration = results[2];

            if (configuration?.status === 'fulfilled' && configuration.value && typeof configuration.value === 'object') {
                const value = configuration.value as ConfigurationSnapshot;

                if (
                    Number.isSafeInteger(value.revision) &&
                    value.configuration?.plugins &&
                    typeof value.configuration.plugins === 'object'
                ) {
                    this.store.configuration = value;
                    this.revision = value.revision;
                }
            }

            this.metadataAt = Date.now();
            this.changed();
        } finally {
            if (generation === this.generation) {
                this.metadataBusy = false;
            }
        }
    }

    private async refreshWireLimit(generation: number, force: boolean): Promise<void> {
        const device = this.store.device('wireview-pro2', '', Date.now());

        if (
            !device ||
            !this.api ||
            this.limitBusy ||
            (!force && device === this.store.wireLimitDevice && Date.now() - this.limitAt < 30000)
        ) {
            return;
        }

        this.limitBusy = true;

        if (device !== this.store.wireLimitDevice) {
            this.store.wireName = '';
        }

        this.store.wireLimit = null;
        this.store.wireLimitDevice = null;

        try {
            const value = (await this.api.get('/plugins/wireview-pro2/device/config', this.controller.signal)) as Record<string, unknown>;

            if (generation !== this.generation || device !== this.store.device('wireview-pro2', '', Date.now())) {
                return;
            }

            const limit = value.wireOcpFaultThresholdA;
            this.store.wireLimit = typeof limit === 'number' && Number.isFinite(limit) && limit > 0 ? limit : null;
            this.store.wireLimitDevice = device;
            this.store.wireName = typeof value.friendlyName === 'string' ? value.friendlyName : '';
        } catch {
            // An unavailable protection limit must not be replaced by a display-axis maximum.
        } finally {
            if (generation === this.generation) {
                this.limitBusy = false;
                this.limitAt = Date.now();
                this.changed();
            }
        }
    }
}