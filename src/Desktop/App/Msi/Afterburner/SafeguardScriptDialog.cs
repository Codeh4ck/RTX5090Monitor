using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Plugins.Msi.Contracts.Safeguard;
using Rtx5090Monitoring.Presentation.Msi.Afterburner;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

internal sealed class SafeguardScriptDialog(SafeguardScriptDefinition script, SafeguardBinding? previous)
{
    private readonly Dictionary<string, Control> _inputs = new(StringComparer.Ordinal);
    private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };

    public async Task<SafeguardBinding?> ShowAsync(XamlRoot root, string installation, int connector)
    {
        var store = script.SendsMail ? new SafeguardStorage() : null;
        var previousMail = script.SendsMail && previous?.MailId is { } id
            ? store!.Read<SafeguardMailCredential>("mail", id) : null;
        var body = new StackPanel { Spacing = 10 };
        foreach (var field in script.Parameters.Values)
        {
            var control = CreateInput(field, previousMail);
            _inputs.Add(field.Name, control);
            body.Children.Add(control);
        }

        var dialog = new ContentDialog
        {
            Title = script.Name + " - connector " + connector,
            XamlRoot = root,
            Content = new ScrollViewer { Content = body, MaxHeight = 520, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            PrimaryButtonText = "Save parameters",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        if (script.SendsMail)
        {
            var test = new Button { Content = "Send test email" };
            body.Children.Add(test);
            test.Click += async (_, _) =>
            {
                test.IsEnabled = false;
                dialog.IsPrimaryButtonEnabled = false;
                try
                {
                    _message.Text = "Sending test email through the Windows service...";
                    var result = await SubmitMailAsync(store!, previousMail, true);
                    _message.Text = result.Message;
                }
                catch (Exception error) when (IsConfigurationError(error))
                {
                    _message.Text = error.Message;
                }
                finally
                {
                    test.IsEnabled = true;
                    dialog.IsPrimaryButtonEnabled = true;
                }
            };
        }

        body.Children.Add(_message);
        SafeguardBinding? binding = null;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var delay = ReadInteger("DelayMs");
                SafeguardScripts.ValidateDelay(delay);

                // Validate installation paths and PowerShell before provisioning credentials.
                var candidate = await Task.Run(() => AfterburnerSafeguard.Create(installation, connector, script, delay,
                    script.SendsMail ? Guid.NewGuid() : null));
                await AfterburnerSafeguard.ProbeAsync(script);
                if (script.SendsMail)
                {
                    var result = await SubmitMailAsync(store!, previousMail, false);
                    if (!result.Success || result.MailId is null)
                    {
                        throw new InvalidOperationException(result.Message);
                    }

                    candidate = candidate with { MailId = result.MailId };
                }

                binding = candidate;
            }
            catch (Exception error) when (IsConfigurationError(error))
            {
                _message.Text = error.Message;
                args.Cancel = true;
            }
            finally
            {
                deferral.Complete();
            }
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary ? binding : null;
    }

    private Control CreateInput(SafeguardParameterDefinition field, SafeguardMailCredential? previousMail)
    {
        var mail = previousMail?.Settings;
        object? current = field.Name switch
        {
            "DelayMs" => previous?.DelayMs,
            "Host" => mail?.Host,
            "Port" => mail?.Port,
            "Security" => mail?.Security,
            "Authentication" => mail is null ? null : mail.Authenticate ? "Username/password" : "Unauthenticated relay",
            "Username" => mail?.Username,
            "Sender" => mail?.Sender,
            "Recipients" => mail is null ? null : string.Join(", ", mail.Recipients),
            _ => null
        };
        current ??= field.Default;

        return field.Type switch
        {
            SafeguardParameterType.Integer => new NumberBox
            {
                Header = field.Label, Minimum = field.Minimum!.Value, Maximum = field.Maximum!.Value,
                Value = (int)current!, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
            },
            SafeguardParameterType.Choice => new ComboBox
            {
                Header = field.Label, ItemsSource = field.Choices, SelectedItem = current,
                HorizontalAlignment = HorizontalAlignment.Stretch
            },
            SafeguardParameterType.Secret => new PasswordBox
            {
                Header = field.Label, MaxLength = 2048,
                PlaceholderText = previousMail?.EncryptedPassword is null ? "" : "Leave blank to keep the saved password"
            },
            _ => new TextBox { Header = field.Label, Text = current as string ?? "", MaxLength = 4096 }
        };
    }

    private int ReadInteger(string field)
    {
        var input = (NumberBox)_inputs[field];
        if (!AfterburnerNumericInput.Commit(input) || !double.IsFinite(input.Value) || Math.Truncate(input.Value) != input.Value)
        {
            throw new ArgumentException("Enter a whole number for " + input.Header + ".");
        }

        return checked((int)input.Value);
    }

    private async Task<SafeguardMailResult> SubmitMailAsync(SafeguardStorage store, SafeguardMailCredential? previousMail, bool test)
    {
        string Read(string key) => ((TextBox)_inputs[key]).Text.Trim();
        string Choice(string key) => (string)((ComboBox)_inputs[key]).SelectedItem;
        var settings = new SafeguardMailSettings(Read("Host"), ReadInteger("Port"), Choice("Security"),
            Choice("Authentication") == "Username/password", Read("Username"), Read("Sender"),
            Read("Recipients").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var password = ((PasswordBox)_inputs["Password"]).Password;
        var reusePassword = previousMail?.EncryptedPassword is not null
            && string.Equals(previousMail.Settings.Host, settings.Host, StringComparison.OrdinalIgnoreCase)
            && previousMail.Settings.Username == settings.Username;
        SafeguardScripts.ValidateMail(settings, password.Length > 0 || reusePassword);

        string? encrypted = null;
        if (settings.Authenticate && password.Length > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            try
            {
                encrypted = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        var id = Guid.NewGuid();
        store.Write("requests", id, new SafeguardMailRequest(DateTimeOffset.UtcNow, settings, encrypted,
            reusePassword ? previous?.MailId : null, test));

        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            while (!deadline.IsCancellationRequested)
            {
                if (store.Read<SafeguardMailResult>("results", id) is { } result)
                {
                    store.Delete("results", id);
                    return result;
                }

                await Task.Delay(250, deadline.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // The service may be stopped or the SMTP server may be unreachable.
        }
        finally
        {
            store.Delete("requests", id);
        }

        return new(false, "No SMTP response from the Windows service. Check that it is running and try again.");
    }

    internal static bool IsConfigurationError(Exception error) => error is IOException or UnauthorizedAccessException
        or ArgumentException or InvalidOperationException or CryptographicException or System.ComponentModel.Win32Exception
        or System.Text.Json.JsonException or OverflowException;
}