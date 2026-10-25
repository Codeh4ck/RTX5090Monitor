using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Rtx5090Monitoring.Contracts.Discovery;
using Rtx5090Monitoring.Contracts.Enums;
using Rtx5090Monitoring.Contracts.Configuration;
using Rtx5090Monitoring.Core.Utilities;

namespace Rtx5090Monitoring.Core.Plugins;

internal sealed class PluginWorker
{
    private readonly ISensorPluginFactory _factory;
    private readonly SensorStateStore _store;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly AsyncSignal _wake = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _cancellationLock = new();
    private CancellationTokenSource? _pollCancellation;
    private PluginSettings _settings = new() { Enabled = false };
    private volatile bool _settingsApplied;
    private ISensorPlugin? _plugin;
    private Task _loop = Task.CompletedTask;

    public PluginWorker(ISensorPluginFactory factory, SensorStateStore store, TimeProvider clock, ILogger logger)
    {
        _factory = factory;
        _store = store;
        _clock = clock;
        _logger = logger;
        _store.Register(Describe(_settings));
    }

    public void Start() => _loop = Task.Run(RunAsync);

    public PluginSettings CreateDefaultSettings(IConfigurationSection? bootstrap = null)
    {
        var defaults = _factory.CreateDefaultSettings();
        if (bootstrap is null)
        {
            return defaults;
        }

        var options = bootstrap.GetSection("Options");

        return new PluginSettings
        {
            Enabled = bootstrap.GetValue("Enabled", defaults.Enabled),
            PollIntervalMs = bootstrap.GetValue("PollIntervalMs", defaults.PollIntervalMs),
            StaleAfterMs = bootstrap.GetValue("StaleAfterMs", defaults.StaleAfterMs),
            Options = options.Exists() ? _factory.ReadBootstrapOptions(options) : defaults.Options
        };
    }

    public void ValidateSettings(PluginSettings settings) => _factory.ValidateSettings(settings);

    public async Task ApplyAsync(PluginSettings settings, CancellationToken cancellationToken)
    {
        if (_settingsApplied && settings.HasSameValues(Volatile.Read(ref _settings)))
        {
            return;
        }

        if (!settings.Enabled)
        {
            CancelCurrentPoll();
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_settingsApplied && settings.HasSameValues(_settings))
            {
                return;
            }

            _settingsApplied = false;
            _store.Register(Describe(settings));

            if (!settings.Enabled)
            {
                _store.SetLifecycle(_factory.Id, PluginState.Stopping);
                _plugin?.Dispose();
                _plugin = null;
                _store.SetLifecycle(_factory.Id, PluginState.Disabled);
            }
            else
            {
                if (_plugin is null)
                {
                    _store.SetLifecycle(_factory.Id, PluginState.Starting);
                    _plugin = _factory.Create(settings);
                }

                _plugin.ApplySettings(settings);
            }

            _settings = settings;
            _settingsApplied = true;
        }
        finally
        {
            _gate.Release();
            _wake.Set();
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<ISensorPlugin, T> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _plugin is null ? throw new PluginUnavailableException($"Plugin '{_factory.Id}' is disabled.") : action(_plugin);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        CancelCurrentPoll();

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_settings.Enabled)
            {
                throw new PluginUnavailableException($"Plugin '{_factory.Id}' is disabled.");
            }

            _store.SetLifecycle(_factory.Id, PluginState.Stopping);
            _settingsApplied = false;
            _plugin?.Dispose();
            _plugin = null;
            _store.SetLifecycle(_factory.Id, PluginState.Starting);
            _plugin = _factory.Create(_settings);
            _settingsApplied = true;
        }
        catch (Exception exception) when (exception is not PluginUnavailableException)
        {
            _store.ReportError(_factory.Id, "Plugin restart failed; see service logs.");
            _logger.LogWarning(exception, "Plugin {PluginId} failed to restart", _factory.Id);

            throw;
        }
        finally
        {
            _gate.Release();
            _wake.Set();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stop.CancelAsync();

        _wake.Set();

        await _loop.WaitAsync(cancellationToken);

        _stop.Dispose();
        _gate.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await PollAsync();

                var interval = _settings.Enabled ? _settings.PollIntervalMs : 30000;
                await _wake.WaitAsync(interval, _stop.Token);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        finally
        {
            await _gate.WaitAsync();
            try
            {
                _plugin?.Dispose();
                _plugin = null;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private async Task PollAsync()
    {
        await _gate.WaitAsync(_stop.Token);

        try
        {
            if (_plugin is null)
            {
                return;
            }

            using var poll = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            lock (_cancellationLock)
            {
                _pollCancellation = poll;
            }

            try
            {
                var sample = await _plugin.PollAsync(poll.Token);
                poll.Token.ThrowIfCancellationRequested();
                _store.Update(_factory.Id, sample, _clock.GetUtcNow());
            }
            catch (OperationCanceledException) when (poll.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _store.ReportError(_factory.Id, "Polling failed; see service logs.");
                _logger.LogWarning(exception, "Plugin {PluginId} failed to poll", _factory.Id);
            }
            finally
            {
                lock (_cancellationLock)
                {
                    _pollCancellation = null;
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void CancelCurrentPoll()
    {
        lock (_cancellationLock)
        {
            _pollCancellation?.Cancel();
        }
    }

    private PluginDescriptor Describe(PluginSettings settings) => new(_factory.Id, _factory.Name,
        TimeSpan.FromMilliseconds(settings.PollIntervalMs), TimeSpan.FromMilliseconds(settings.StaleAfterMs));
}
