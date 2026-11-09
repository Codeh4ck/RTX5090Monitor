using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Rtx5090Monitoring.Contracts.Sensors;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Safeguard;

namespace Rtx5090Monitoring.Service.Services;

internal sealed class SafeguardJournalService(TimeProvider clock, ILogger<SafeguardJournalService> logger) : BackgroundService
{
    private SafeguardExecutionNotice[]? _notices;
    public SafeguardExecutionNotice[]? Notices => Volatile.Read(ref _notices);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await Task.WhenAll(
            PollAsync(RefreshNoticesAsync, TimeSpan.FromSeconds(1), stoppingToken),
            PollAsync(ProcessMailAsync, TimeSpan.FromSeconds(1), stoppingToken));
    }

    [SupportedOSPlatform("windows")]
    private async Task PollAsync(Func<SafeguardStorage, CancellationToken, Task> poll, TimeSpan interval, CancellationToken token)
    {
        SafeguardStorage? storage = null;
        using var timer = new PeriodicTimer(interval, clock);
        do
        {
            try
            {
                if (Directory.Exists(SafeguardStorage.DefaultRoot))
                {
                    storage ??= new SafeguardStorage();
                    await poll(storage, token);
                }
            }
            catch (Exception error) when (IsRecordError(error))
            {
                logger.LogWarning("Safeguard+ journal could not be read or updated ({Type}).", error.GetType().Name);
                storage = null;
                await Task.Delay(TimeSpan.FromSeconds(15), clock, token);
            }
        }
        while (await timer.WaitForNextTickAsync(token));
    }

    [SupportedOSPlatform("windows")]
    private Task RefreshNoticesAsync(SafeguardStorage storage, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-storage.RetentionDays);
        var notices = new List<SafeguardExecutionNotice>();

        foreach (var id in storage.Ids("history"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var execution = storage.Read<SafeguardExecution>("history", id);
                if (execution is null || execution.Id != id || execution.Connector is not (1 or 2))
                {
                    continue;
                }

                if (execution.StartedAt < cutoff && !SafeguardStorage.IsRunning(execution, now))
                {
                    storage.Delete("history", id);
                    continue;
                }

                var status = SafeguardStorage.DisplayStatus(execution, now);
                notices.Add(new(id, execution.Name, execution.Connector, execution.StartedAt, status));
            }
            catch (Exception error) when (IsRecordError(error))
            {
                logger.LogWarning("Could not read Safeguard+ execution {ExecutionId} ({Type}).", id, error.GetType().Name);
            }
        }

        Volatile.Write(ref _notices, notices.OrderBy(notice => notice.StartedAt).ThenBy(notice => notice.Id).ToArray());
        return Task.CompletedTask;
    }

    [SupportedOSPlatform("windows")]
    private async Task ProcessMailAsync(SafeguardStorage storage, CancellationToken token)
    {
        var now = clock.GetUtcNow();
        foreach (var id in storage.Ids("requests"))
        {
            token.ThrowIfCancellationRequested();
            await HandleMailRequestAsync(storage, id, now, token);
        }

        foreach (var id in storage.Ids("outbox"))
        {
            token.ThrowIfCancellationRequested();
            await DeliverAlarmAsync(storage, id, now, token);
        }

        foreach (var id in storage.Ids("results"))
        {
            if (File.GetLastWriteTimeUtc(storage.Location("results", id)) < now.UtcDateTime.AddHours(-1))
            {
                storage.Delete("results", id);
            }
        }

        var cutoff = now.AddDays(-storage.RetentionDays);
        foreach (var id in storage.Ids("delivery"))
        {
            if (File.GetLastWriteTimeUtc(storage.Location("delivery", id)) < cutoff.UtcDateTime
                && !File.Exists(storage.Location("history", id)) && !File.Exists(storage.Location("outbox", id)))
            {
                storage.Delete("delivery", id);
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task HandleMailRequestAsync(SafeguardStorage storage, Guid id, DateTimeOffset now, CancellationToken token)
    {
        SafeguardMailResult result;

        try
        {
            var request = storage.Read<SafeguardMailRequest>("requests", id);
            if (request is null)
            {
                return;
            }

            if (request.CreatedAt < now.AddMinutes(-2) || File.Exists(storage.Location("results", id)))
            {
                storage.Delete("requests", id);
                return;
            }

            var previous = request.PreviousMailId is { } previousId
                ? storage.Read<SafeguardMailCredential>("mail", previousId) : null;
            string? encryptedPassword = null;
            if (request.Settings.Authenticate)
            {
                if (request.EncryptedInput is { } encryptedInput)
                {
                    var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedInput), null, DataProtectionScope.LocalMachine);
                    try
                    {
                        encryptedPassword = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(bytes);
                    }
                }
                else if (previous?.Settings is { } old && string.Equals(old.Host, request.Settings.Host, StringComparison.OrdinalIgnoreCase)
                    && old.Username == request.Settings.Username)
                {
                    encryptedPassword = previous.EncryptedPassword;
                }
            }

            SafeguardScripts.ValidateMail(request.Settings, encryptedPassword is not null);
            var credentials = new SafeguardMailCredential(request.Settings, encryptedPassword);
            if (request.SendTest)
            {
                await SendAsync(credentials, "RTX Monitor Safeguard+ SMTP test",
                    "RTX Monitor sent this test email. This was a test of email delivery.", null, token);
                result = new(true, "SMTP server accepted the test email.");
            }
            else
            {
                storage.Write("mail", id, credentials);
                result = new(true, "SMTP settings validated and saved.", id);
            }
        }
        catch (Exception error) when (!token.IsCancellationRequested && IsMailError(error))
        {
            result = new(false, DescribeMailError(error));
        }

        storage.Write("results", id, result);
        storage.Delete("requests", id);
    }

    [SupportedOSPlatform("windows")]
    private async Task DeliverAlarmAsync(SafeguardStorage storage, Guid id, DateTimeOffset now, CancellationToken token)
    {
        var attempt = 1;
        try
        {
            var delivery = storage.Read<SafeguardDelivery>("delivery", id);
            if (delivery?.Status is "Accepted by SMTP server" or "Delivery expired")
            {
                storage.Delete("outbox", id);
                return;
            }

            var job = storage.Read<SafeguardMailEvent>("outbox", id);
            if (job is null)
            {
                return;
            }

            attempt = (delivery?.Attempts ?? 0) + 1;
            if (job.StartedAt < now.AddDays(-7))
            {
                storage.Write("delivery", id, new SafeguardDelivery("Delivery expired", attempt - 1, now));
                storage.Delete("outbox", id);
                return;
            }

            if (delivery?.NextAttempt > now)
            {
                return;
            }

            var binding = storage.Read<SafeguardBinding>("bindings", job.BindingId);
            if (job.ExecutionId != id || binding is null || binding.ScriptId != SafeguardScripts.Notify || binding.MailId is not { } mailId)
            {
                throw new InvalidDataException("The email request has no saved notification binding.");
            }

            binding.Validate();
            var mail = storage.Read<SafeguardMailCredential>("mail", mailId)
                ?? throw new InvalidDataException("Saved SMTP configuration is unavailable.");
            var execution = storage.Read<SafeguardExecution>("history", id);
            var status = execution is null ? "Triggered" : SafeguardStorage.DisplayStatus(execution, now);
            var details = execution is null ? "" : string.Join(Environment.NewLine,
                execution.Steps.Select(step => $"{step.Name}: {step.Result}"));
            var body = $"Safeguard+ ran on {Environment.MachineName}.\n"
                + $"Connector: {binding.Connector}\nTriggered: {job.StartedAt:O}\n"
                + $"Script: {SafeguardScripts.Get(binding.ScriptId).Name}\nExecution: {id:N}\n"
                + $"Shutdown delay: {binding.DelayMs} ms\nStatus at send time: {status}\n{details}";
            await SendAsync(mail, "RTX Monitor Safeguard+ triggered", body, id, token);
            storage.Write("delivery", id, new SafeguardDelivery("Accepted by SMTP server", attempt, now));
            storage.Delete("outbox", id);
        }
        catch (Exception error) when (!token.IsCancellationRequested && IsMailError(error))
        {
            var wait = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, Math.Min(attempt - 1, 6))));
            storage.Write("delivery", id, new SafeguardDelivery("Retry pending", attempt, now + wait, DescribeMailError(error)));
        }
    }

    [SupportedOSPlatform("windows")]
    private static async Task SendAsync(SafeguardMailCredential mail, string subject, string body, Guid? id, CancellationToken token)
    {
        var settings = mail.Settings;
        SafeguardScripts.ValidateMail(settings, mail.EncryptedPassword is not null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));

        using var message = new MimeMessage { Subject = subject, Body = new TextPart("plain") { Text = body } };
        if (id is { } executionId)
        {
            message.MessageId = $"safeguard-{executionId:N}@rtx-monitor.local";
        }

        message.From.Add(MailboxAddress.Parse(settings.Sender));
        foreach (var recipient in settings.Recipients)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        using var client = new SmtpClient();
        var mode = settings.Security == "Implicit TLS" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(settings.Host, settings.Port, mode, deadline.Token);
        if (settings.Authenticate)
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(mail.EncryptedPassword!), null, DataProtectionScope.CurrentUser);
            try
            {
                await client.AuthenticateAsync(settings.Username, Encoding.UTF8.GetString(bytes), deadline.Token);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        await client.SendAsync(message, deadline.Token);
    }

    private static bool IsRecordError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException;

    private static bool IsMailError(Exception error) => IsRecordError(error)
        || error is ArgumentException or FormatException or CryptographicException or OperationCanceledException
            or MailKit.Security.AuthenticationException or MailKit.Security.SslHandshakeException
            or SmtpCommandException or System.Net.Sockets.SocketException or InvalidOperationException;

    private static string DescribeMailError(Exception error) => error switch
    {
        MailKit.Security.AuthenticationException => "SMTP authentication failed. Check the username and password.",
        MailKit.Security.SslHandshakeException => "SMTP TLS negotiation failed. Check the server name, certificate and TLS mode.",
        SmtpCommandException smtp => "SMTP rejected the request (" + (int)smtp.StatusCode + ").",
        OperationCanceledException => "SMTP operation timed out.",
        CryptographicException => "The Windows service could not decrypt the saved SMTP password.",
        ArgumentException => "SMTP configuration is incomplete or invalid.",
        _ => "SMTP request failed (" + error.GetType().Name + ")."
    };
}