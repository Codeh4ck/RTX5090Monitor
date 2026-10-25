# RTX 5090 Monitoring

.NET 10 service with independently managed WireView, ASUS GPU and MSI Ai1300TS / Ai1600TS PSU plugins, live JSON configuration, a typed shared WebSocket stream, and original plugin-specific payloads/endpoints. The Xeneon widget workspace remains inert.

The desktop [Debug Console](docs/debug-console.md) is available in Debug builds or with `EnableTelemetryDebug=true`; ordinary Release packages exclude it. Open it from Configuration → Maintenance for side-by-side local telemetry simulation.

The MSI plugin publishes verified PSU-side pin currents and cooling telemetry, with optional fan control. See [MSI support, configuration and hardware verification](docs/msi-psu.md).

The WinUI 3 desktop client and full configurator are under the solution's **UI** folder. See [desktop setup and architecture](docs/desktop-app.md) for publishing, simulated-data mode, tray/startup behavior and configuration.

The **Installer** folder builds an offline setup EXE with a required local service, optional desktop app, Modify/Repair/Uninstall and configuration-preserving upgrade rollback. Run `./scripts/Build-Release.ps1 -Version 0.3.1` to create the installer/widget ZIP. See [release and uninstall instructions](docs/releases.md). Building a release does not install it.

## Service endpoints

All routes use `localhost:59273`. The original WireView service must release that port before this host can bind it.

| Endpoint | Purpose |
|---|---|
| GET `/health` | Service liveness, uptime, plugin states, effective configuration revision/error |
| GET `/plugins` | Installed plugins, enablement, availability and supported capabilities |
| WS `/sensors` | Full typed multi-plugin snapshots, including `configurationRevision` |
| GET `/configuration` | Effective settings, revision, version/ETag and last rejected-file error |
| PUT `/configuration` | Apply configuration; optional `If-Match` rejects stale writes with HTTP 412 |
| GET `/plugins/{pluginId}/settings` | Current options for an installed plugin |
| PUT `/plugins/{pluginId}/settings` | Apply plugin enablement, polling and freshness options |
| POST `/restart` | Restart backend service and all enabled plugins through configured Windows Service recovery |

A normal GET to the shared `/sensors` route requires a WebSocket upgrade and returns 400 otherwise. Client command messages are not accepted on sensor sockets.

## WireView endpoint parity

Prefix every WireView route below with `/plugins/wireview-pro2`:

| Endpoint | Original behavior provided |
|---|---|
| GET `/health` | `ok` / `no_device`; additionally `disabled` when explicitly disabled |
| GET `/sensors` | Original six-pin structured snapshot and field names |
| WS `/ws` | Original structured WireView payload, including initial latest snapshot |
| GET `/config` | `{ "pollIntervalMs": 1000 }` |
| POST `/config` | Update polling interval immediately; preserve original 100-30000 ms clamping |
| GET `/device/config` | Read parsed device settings, including thresholds, fan settings and fault-enable masks |
| GET `/device/config/raw` | Read raw configuration as `{ "bytes": "...", "length": ... }` |
| POST `/faults/clear` | Independently clear requested status/log bits using the original preserve-mask protocol |
| POST `/restart` | Dispose and recreate only the WireView plugin |

Health and configuration remain available while WireView is disabled. Sensor/device operations report 503 while disabled/disconnected. Device read/write failures report an error instead of pretending success. A successful fault-clear response means the command was transmitted; a continuing physical fault can be reported again by the device.

The old root aliases `/config`, `/ws`, `/device/config`, and `/faults/clear` are not installed. Existing widgets need their base URL changed to include the plugin prefix. Global health and full-host restart are host responsibilities; the shared `/sensors` WebSocket retains the new typed schema.

## Live settings: JSON only

The authoritative file is `%ProgramData%\Rtx5090Monitoring\appsettings.json`. No registry settings are read, written, or migrated by this host.

```json
{
  "monitoring": {
    "streamIntervalMs": 1000,
    "selections": []
  },
  "plugins": {
    "asus-astral": {
      "enabled": true,
      "pollIntervalMs": 1000,
      "staleAfterMs": 5000,
      "options": {
        "deviceId": null,
        "imbalanceAmperageThreshold": 6,
        "imbalancePercentageThreshold": 30,
        "imbalanceDurationThresholdMs": 3000,
        "wireOvercurrentAmperageThreshold": 9.2,
        "wireOvercurrentThresholdMs": 3000
      }
    },
    "wireview-pro2": {
      "enabled": true,
      "pollIntervalMs": 1000,
      "staleAfterMs": 5000
    }
  }
}
```

The project's `Rtx5090Monitoring.Service/appsettings.json` seeds this file only when it does not exist. Once created, edit the runtime file or use the configuration endpoints. A bootstrap `RuntimeConfiguration:FilePath` setting can select another absolute path for development/testing.

API writes validate and atomically replace the file, apply changes, then return the effective revision. Failed plugin activation attempts roll back the configuration. Direct edits and atomic file replacements are observed by a file watcher, with a one-second recovery scan for missed events. Invalid live edits retain the previous effective configuration and expose an error through `/configuration` and `/health`. At startup, an invalid persisted file is rejected because no active last-valid configuration exists yet.

Polling and stream delays have explicit wake signals: changes do not wait for a previous 30-second delay to expire. An active hardware transaction is drained before its plugin is disposed/reconfigured. Enable/disable cycles construct and dispose plugin instances without restarting the service or closing the sensor stream. The WireView-specific stream closes cleanly when that device/plugin becomes unavailable; clients can reconnect after recovery.

`PUT /configuration` accepts the **configuration object**, not the surrounding GET response envelope. `PUT /plugins/{pluginId}/settings` accepts a complete plugin settings object:

```json
{ "enabled": false, "pollIntervalMs": 1000, "staleAfterMs": 5000 }
```

Polling intervals accept 100-30000 ms; freshness must be at least the polling interval and at most 300000 ms. Source selection, stream cadence and freshness changes also apply live. Configuration must name exactly the installed plugin IDs. The file is the durable authority; command-line/environment defaults do not override its mutable values after initialization.

Device configuration remains on WireView hardware. Its thresholds/fan settings are read from the device, while polling and service/plugin options live in JSON. Device configuration writes and firmware updates are not implemented, matching the original service's scope.

## ASUS endpoint parity

The original standalone ASUS service is available under `/plugins/asus-astral`: GET `/sensors`, WS `/ws`, GET/PUT `/config`, and POST `/restart`. Added GET `/health` reports plugin state, and GET `/devices` lists matching hardware identifiers for GPU selection. All routes share port 59273; there is no ASUS listener on 59275.

ASUS is enabled by default and monitors one GPU, defaulting to the first match of the original 5090/5080 filter. It retains the original 32 sensor mappings and legacy payload fields. Imbalance clears on recovery; overcurrent duration is tracked independently per pin. The common stream includes ASUS readings and typed warning metrics, with identities distinct from WireView.

ASUS thresholds and `deviceId` live under its plugin settings' `options` object and reload live. Startup adds defaults for newly installed plugins to an existing runtime file while preserving existing settings. API updates and live file edits still require all installed IDs. See [the ASUS reference](docs/asus-plugin.md) for the complete JSON example, endpoint details, validation bounds and behavior changes.

## Build and checks

From this directory:

```powershell
dotnet restore Rtx5090Monitoring.slnx
dotnet build Rtx5090Monitoring.slnx --no-restore
dotnet run --project Rtx5090Monitoring.Tests --no-build
```

Tests host Kestrel on ephemeral loopback ports and use fake readers/factories. They cover live configuration, lifecycle, source selection, protocol configuration parsing, serialization, endpoint parity and WebSocket behavior without accessing USB/GPU hardware, restarting installed services or writing ProgramData/registry settings.

To run the real host:

```powershell
dotnet run --project Rtx5090Monitoring.Service
```

WireView and ASUS are enabled by default. Set both plugins to `enabled: false` for a no-hardware session. The Windows Service identity is `Rtx5090MonitoringService`. Actual service installation/recovery configuration is separate; POST `/restart` exits with code 1 after 500 ms so an installed service's recovery policy can relaunch it. An interactive console process exits without relaunching itself.

## Architecture and remaining work

See [the architecture reference](docs/plugin-architecture.md) and [ASUS plugin reference](docs/asus-plugin.md). MSI adapters and widget rendering remain future work. Adding a new compiled plugin requires deployment; toggling an already installed plugin does not. The WireView adapter currently identifies a device by COM port and selects one available device; persistent USB identity and multi-device support remain future extensions. No physical hardware validation was performed as part of this implementation.
