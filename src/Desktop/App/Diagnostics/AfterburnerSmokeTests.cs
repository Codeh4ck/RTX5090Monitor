using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Reflection;
using RTXLens.App.Msi.Afterburner;
using RTXLens.App.Services;
using RTXLens.App.Views;
using RTXLens.Presentation.Msi.Afterburner;

namespace RTXLens.App.Diagnostics;

internal static class AfterburnerSmokeTests
{
    internal static async Task RunAsync(MainWindow window, AppRuntime runtime, string output)
    {
        window.Navigate("configuration");
        var configuration = (ConfigurationView)window.CurrentView!;
        configuration.SelectSection("plugin:msi-psu");
        await Task.Delay(1200);
        var msi = runtime.Configuration.Plugins.Single(plugin => plugin.IsMsi);
        var launcher = VisualTreeQuery.Descendants<Button>(configuration).Single(button => button.Name == "AfterburnerIntegrationButton");
        msi.Enabled = false;
        if (launcher.IsEnabled)
        {
            throw new InvalidOperationException("The integration launcher must be disabled with the MSI plugin.");
        }

        msi.Enabled = true;
        if (!launcher.IsEnabled || VisualTreeQuery.Descendants<AfterburnerSettingsView>(configuration).Any())
        {
            throw new InvalidOperationException("The integration must open from the enabled MSI header button instead of rendering inline.");
        }

        await SmokeTestRunner.CaptureElementAsync(launcher, Path.Combine(output, "afterburner-launcher.png"));
        await runtime.OpenAfterburnerSettingsAsync();
        await Task.Delay(1200);
        var integrationWindow = runtime.AfterburnerWindow ?? throw new InvalidOperationException("The integration window did not open.");
        var integration = integrationWindow.Integration;
        var panels = (Grid)integration.FindName("Panels");
        var left = (StackPanel)integration.FindName("LeftColumn");
        var rightColumn = (StackPanel)integration.FindName("RightColumn");
        var pageScroll = (ScrollViewer)integration.FindName("PageScroll");
        var configurationBody = (FrameworkElement)integration.FindName("ConfigurationBody");
        var connectorPanel = (FrameworkElement)integration.FindName("ConnectorConfigurationPanel");
        var backupsPanel = (FrameworkElement)integration.FindName("BackupsPanel");
        if (panels.Children.Count != 2 || Grid.GetColumn(rightColumn) != 1
            || left.Children.Count != 2 || !ReferenceEquals(left.Children[1], connectorPanel)
            || !ReferenceEquals(rightColumn.Children[0], backupsPanel))
        {
            throw new InvalidOperationException("Configuration/connector panels belong on the left, with a separate backup panel on the right.");
        }

        if (pageScroll.ScrollableHeight > 1 || connectorPanel.VerticalAlignment != VerticalAlignment.Top
            || backupsPanel.VerticalAlignment != VerticalAlignment.Top)
        {
            throw new InvalidOperationException("Content-sized panels must fit the default height instead of expanding with the window.");
        }

        var backupList = (ListView)integration.FindName("BackupPicker");
        var headers = (Grid)integration.FindName("BackupColumnHeaders");
        var backupActions = (StackPanel)integration.FindName("BackupActions");
        if (headers.ColumnDefinitions.Count != 2 || backupList.ItemTemplate is null
            || backupActions.Orientation != Orientation.Horizontal
            || backupActions.Children.OfType<Button>().Select(button => button.Content?.ToString()).SequenceEqual(new[] { "Save config", "Restore config", "Delete" }) != true
            || integration.FindName("BackupPrompt") is not null)
        {
            throw new InvalidOperationException("Backups require a two-column list, a single compact action row, and help through an info button.");
        }
        var header = (Grid)integration.FindName("HeaderLayout");
        var leftEdge = panels.TransformToVisual(integration).TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        var rightGap = integration.ActualWidth - leftEdge - panels.ActualWidth;
        if (Math.Abs(leftEdge - rightGap) > 1 || Math.Abs(header.ActualWidth - panels.ActualWidth) > 1)
        {
            throw new InvalidOperationException("The header and panels must share equal left/right outer margins and column widths.");
        }

        var runningWarning = (FrameworkElement)integration.FindName("ProcessWarning");
        var headerInfo = (FrameworkElement)integration.FindName("HeaderInfoButton");
        var warningEnd = runningWarning.TransformToVisual(integration).TransformPoint(new Windows.Foundation.Point(runningWarning.ActualWidth, 0)).X;
        var infoStart = headerInfo.TransformToVisual(integration).TransformPoint(new Windows.Foundation.Point(0, 0)).X;
        if (Math.Abs(infoStart - warningEnd - 12) > 1 || backupList.SelectionMode != ListViewSelectionMode.Multiple
            || backupList.IsMultiSelectCheckBoxEnabled)
        {
            throw new InvalidOperationException("The warning must sit beside its info button, and backups must retain multiselect without a check column.");
        }

        var launcherIcon = VisualTreeQuery.Descendants<Controls.WidgetIcon>(launcher).Single();
        if (launcherIcon.IconName != "settings" || launcherIcon.Width < 22)
        {
            throw new InvalidOperationException("The launcher needs a readable dark SVG on its light icon surface.");
        }

        if (AfterburnerFileStore.DefaultBackupRoot != Path.Combine(AppContext.BaseDirectory, "AfterburnerBackups"))
        {
            throw new InvalidOperationException("The active backup root must be beside the RTX Lens executable.");
        }
        var softwareCheckbox = (CheckBox)integration.FindName("SoftwareAlarmsEnabled");
        var checkboxRow = (Grid)softwareCheckbox.Parent;
        if (checkboxRow.ColumnDefinitions[0].Width.GridUnitType != GridUnitType.Auto || checkboxRow.HorizontalAlignment != HorizontalAlignment.Left)
        {
            throw new InvalidOperationException("The software-alarm info button must sit directly beside the checkbox.");
        }

        var expectedWindowHeight = integration.PreferredContentHeight;
        if (integration.ActualHeight - expectedWindowHeight > 12)
        {
            throw new InvalidOperationException("The initial window must size to its contents without a large unused bottom gap.");
        }

        var originalBackups = backupList.ItemsSource;
        var originalBackupSelection = backupList.SelectedItem;
        backupList.ItemsSource = Enumerable.Range(0, 12)
            .Select(index => new AfterburnerBackupChoice("fixture-" + index, "2026-11-08 12:00:00", "Saved working configuration")).ToArray();
        await Task.Delay(250);
        var listScroll = VisualTreeQuery.Descendants<ScrollViewer>(backupList).First();
        if (backupList.ActualHeight > 290.5 || listScroll.ScrollableHeight <= 0
            || Math.Abs(listScroll.ViewportHeight - 6 * 48) > 1)
        {
            throw new InvalidOperationException($"The backup list must fit six complete rows and scroll the rest. Height={backupList.ActualHeight}, viewport={listScroll.ViewportHeight}, scroll={listScroll.ScrollableHeight}.");
        }

        backupList.SelectedItems.Clear();
        backupList.SelectedItems.Add(backupList.Items[0]);
        backupList.SelectedItems.Add(backupList.Items[1]);
        var restore = (Button)integration.FindName("RestoreButton");
        var delete = (Button)integration.FindName("DeleteBackupButton");
        if (backupList.SelectedItems.Count != 2 || restore.IsEnabled || !delete.IsEnabled)
        {
            throw new InvalidOperationException("Selecting multiple backups must enable deletion and disable restore.");
        }
        backupList.ItemsSource = originalBackups;
        backupList.SelectedItem = originalBackupSelection;
        await Task.Delay(150);
        if (Math.Abs(backupList.ActualHeight - 290) > 1 || integration.HasPendingChanges)
        {
            throw new InvalidOperationException("A freshly loaded configuration must not show pending edits.");
        }
        var editors = new[]
        {
            (AfterburnerConnectorView)((ContentControl)integration.FindName("Connector1Host")).Content,
            (AfterburnerConnectorView)((ContentControl)integration.FindName("Connector2Host")).Content
        };
        if (editors.Any(editor => editor.Saved is null))
        {
            throw new InvalidOperationException("Each connector must have its own populated configuration table.");
        }
        var timeoutFields = editors.Select(editor => VisualTreeQuery.Descendants<NumberBox>(editor)
            .Single(field => field.Name == "Connector" + editor.Connector + "AlarmTimeout")).ToArray();
        for (var index = 0; index < editors.Length; index++)
        {
            var expected = editors[index].Saved!.AlarmTimeoutMs;
            var actual = editors[index].Draft.AlarmTimeoutMs;
            if (actual != expected)
            {
                throw new InvalidOperationException("Each timeout field must load its own connector's native value.");
            }
        }

        var firstTimeout = timeoutFields[0].Value;
        var secondTimeout = editors[1].Draft.AlarmTimeoutMs;
        timeoutFields[0].Value = 2345.6;
        if (editors[0].Draft.AlarmTimeoutMs != 2346 || editors[1].Draft.AlarmTimeoutMs != secondTimeout)
        {
            throw new InvalidOperationException("Timeout edits must use whole milliseconds without changing the other connector.");
        }

        var connectorTabs = (TabView)integration.FindName("ConnectorConfigurationTabs");
        connectorTabs.SelectedIndex = 1;
        connectorTabs.SelectedIndex = 0;
        if (editors[0].Draft.AlarmTimeoutMs != 2346)
        {
            throw new InvalidOperationException("Changing connector tabs must retain each timeout draft.");
        }

        timeoutFields[0].Value = firstTimeout;
        var firstCheckbox = VisualTreeQuery.Descendants<CheckBox>(editors[0]).Single(box => box.Name == "Connector1AlarmEnabled");
        var originalEnabled = firstCheckbox.IsChecked;
        firstCheckbox.IsChecked = originalEnabled != true;
        var selector = (TabView)integration.FindName("ConnectorConfigurationTabs");
        selector.SelectedIndex = 1;
        selector.SelectedIndex = 0;
        if (editors[0].Draft.Enabled == (originalEnabled == true))
        {
            throw new InvalidOperationException("Changing connector tabs must not discard the other connector's draft.");
        }

        firstCheckbox.IsChecked = originalEnabled;
        if (!VisualTreeQuery.Descendants<CheckBox>(integration).Any(box => box.Name == "SoftwareAlarmsEnabled")
            || VisualTreeQuery.Descendants<ComboBox>(integration).Any(box => box.Name == "AlarmMode")
            || !VisualTreeQuery.Descendants<HyperlinkButton>(integration).Any(link => link.Name == "BackupLocation"))
        {
            throw new InvalidOperationException("The software-alarm checkbox and Explorer backup link must be available.");
        }
        var buttons = VisualTreeQuery.Descendants<Button>(integration).ToArray();
        if (!buttons.Any(button => button.Name == "SaveButton") || !buttons.Any(button => button.Name == "RestoreButton"))
        {
            throw new InvalidOperationException("The Afterburner integration does not expose paired backup and restore actions.");
        }

        var applyButton = buttons.Single(button => button.Name == "ApplyButton");
        var startButton = buttons.Single(button => button.Name == "RestartButton");
        var message = VisualTreeQuery.Descendants<InfoBar>(integration).Single(bar => bar.Name == "OperationMessage");
        var actionRow = (Grid)VisualTreeHelper.GetParent(applyButton);
        if (!ReferenceEquals(actionRow, VisualTreeHelper.GetParent(startButton)) || Grid.GetColumn(startButton) != 2)
        {
            throw new InvalidOperationException("Apply and Restart must share a row with Restart at the right.");
        }

        var parent = (StackPanel)VisualTreeHelper.GetParent(actionRow);
        if (parent.Children.IndexOf(message) <= parent.Children.IndexOf(actionRow))
        {
            throw new InvalidOperationException("Operation feedback must follow the action buttons.");
        }

        if (!buttons.Any(button => button.Name == "DeleteBackupButton"))
        {
            throw new InvalidOperationException("The selected-backup delete action is missing.");
        }

        var badges = VisualTreeQuery.Descendants<Grid>(integration).Single(grid => grid.Name == "StatusBadges");
        if (badges.Children.Count != 2 || badges.Children.Any(child => child is not Border { BorderThickness.Left: 1, CornerRadius.TopLeft: 5 }))
        {
            throw new InvalidOperationException("Afterburner status readings must use the GPU-style bordered badges.");
        }

        if ((string)startButton.Content != "Restart Afterburner"
            || integration.FindName("ConnectorPicker") is not null
            || integration.FindName("FilesStatus") is not null
            || integration.FindName("RestartStatus") is not null)
        {
            throw new InvalidOperationException("Use a restart action and connector tabs instead of redundant selector/status badges.");
        }

        var installationPicker = (FrameworkElement)integration.FindName("InstallationPicker");
        var badgeBottom = badges.TransformToVisual(configurationBody)
            .TransformPoint(new Windows.Foundation.Point(0, badges.ActualHeight)).Y;
        var pickerTop = installationPicker.TransformToVisual(configurationBody)
            .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        if (badgeBottom > pickerTop || integration.FindName("StatusPanel") is not null
            || integration.FindName("ConnectorStatusPanel") is not null
            || integration.FindName("MonitoringStatus") is not null || integration.FindName("ProcessStatus") is not null)
        {
            throw new InvalidOperationException("Keep only installation badges above the inputs; redundant status panels and process badges must be removed.");
        }

        var warning = (FrameworkElement)integration.FindName("ProcessWarning");
        if ((warning.Visibility == Visibility.Visible) != AfterburnerDiscovery.IsRunning())
        {
            throw new InvalidOperationException("The header running warning must follow the actual process state.");
        }

        var updateStatus = typeof(AfterburnerSettingsView).GetMethod("UpdateInstallationStatus", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var installationField = typeof(AfterburnerSettingsView).GetField("_installation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalInstallation = (AfterburnerInstallation)installationField.GetValue(integration)!;
        updateStatus.Invoke(integration, [null, false]);
        var stateGroups = VisualStateManager.GetVisualStateGroups((FrameworkElement)integration.Content);
        if (stateGroups.Single(group => group.Name == "VersionStates").CurrentState?.Name != "VersionMissing"
            || stateGroups.Single(group => group.Name == "PluginStates").CurrentState?.Name != "PluginMissing")
        {
            throw new InvalidOperationException("Missing Afterburner and PSU.dll must activate their error badge states.");
        }

        updateStatus.Invoke(integration, [originalInstallation, false]);
        // Verify the unknown-process-state guard without starting/stopping Afterburner or requesting elevation.
        var stateField = typeof(AfterburnerSettingsView).GetField("_processStateKnown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var updateButtons = typeof(AfterburnerSettingsView).GetMethod("SetEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stateWasKnown = (bool)stateField.GetValue(integration)!;
        stateField.SetValue(integration, false);
        updateButtons.Invoke(integration, null);
        if (startButton.IsEnabled)
        {
            throw new InvalidOperationException("Restart must be disabled while process state is unknown.");
        }

        stateField.SetValue(integration, stateWasKnown);
        updateButtons.Invoke(integration, null);
        foreach (var field in VisualTreeQuery.Descendants<NumberBox>(integration)
            .Where(field => field.Name is "MaxCurrent" or "CurrentDifference"))
        {
            var originalValue = field.Value;
            for (var step = 0; step < 20; step++)
            {
                field.Value += field.SmallChange;
            }

            for (var step = 0; step < 20; step++)
            {
                field.Value -= field.SmallChange;
            }

            if (field.Value != Math.Round(field.Value, 1, MidpointRounding.AwayFromZero)
                || field.Text != field.NumberFormatter.FormatDouble(field.Value))
            {
                throw new InvalidOperationException("Current increments must retain one decimal place without floating-point display artifacts.");
            }

            field.Value = 9.25;
            if (field.Value != 9.3)
            {
                throw new InvalidOperationException("Typed current values must normalize to one decimal place.");
            }

            field.Value = originalValue;
        }

        var showMessage = typeof(AfterburnerSettingsView).GetMethod("ShowMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        showMessage.Invoke(integration, ["Preview: operation feedback is shown below the action buttons.", false]);
        await Task.Delay(350);
        DependencyObject? ancestor = integration;
        while (ancestor is not null && ancestor is not ScrollViewer)
        {
            ancestor = VisualTreeHelper.GetParent(ancestor);
        }

        if (ancestor is ScrollViewer scroll)
        {
            var top = message.TransformToVisual(scroll).TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
            if (top < -1 || top + message.ActualHeight > scroll.ActualHeight + 1)
            {
                throw new InvalidOperationException($"Operation feedback must be visible. Top={top}, message={message.ActualHeight}, viewport={scroll.ViewportHeight}, actual={scroll.ActualHeight}, offset={scroll.VerticalOffset}, extent={scroll.ScrollableHeight}.");
            }
        }

        var messageTop = message.TransformToVisual(integrationWindow.ThemeRoot)
            .TransformPoint(new Windows.Foundation.Point(0, 0)).Y;
        if (messageTop < -1 || messageTop + message.ActualHeight > integrationWindow.ThemeRoot.ActualHeight + 1)
        {
            throw new InvalidOperationException("The pinned operation feedback must stay visible below the action buttons.");
        }

        var sharedCurrent = (NumberBox)integration.FindName("MaxCurrent");
        var savedCurrent = sharedCurrent.Value;
        sharedCurrent.Value += 0.1;
        if (!integration.HasPendingChanges)
        {
            throw new InvalidOperationException("Shared configuration edits must participate in the close warning.");
        }

        sharedCurrent.Value = savedCurrent;
        timeoutFields[0].Value = (double.IsFinite(firstTimeout) ? firstTimeout : 2000) + 100;
        if (!integration.HasPendingChanges || VisualTreeQuery.Descendants<TextBlock>(editors[0]).Any(text => text.Text == "Unsaved changes"))
        {
            throw new InvalidOperationException("Connector changes need a close confirmation instead of an unsaved label.");
        }

        // WM_CLOSE exercises the same close path as the title-bar X, within this isolated diagnostic process only.
        Platform.Interop.NativeMethods.PostMessageW(
            WinRT.Interop.WindowNative.GetWindowHandle(integrationWindow), 0x0010, 0, 0);
        var closeDialog = await FindPendingDialogAsync(integrationWindow);
        await SmokeTestRunner.CaptureElementAsync(closeDialog, Path.Combine(output, "afterburner-pending-changes.png"));
        InvokeDialogButton(closeDialog, "CloseButton");
        await Task.Delay(250);
        if (runtime.AfterburnerWindow is null || !integration.HasPendingChanges)
        {
            throw new InvalidOperationException("Go back must keep the window open and retain its drafts.");
        }

        timeoutFields[0].Value = firstTimeout;
        if (integration.HasPendingChanges)
        {
            throw new InvalidOperationException("Reverting every field must clear pending changes.");
        }
        await SmokeTestRunner.CaptureElementAsync(integration, Path.Combine(output, "afterburner-integration.png"));
        var installation = AfterburnerDiscovery.Find().Single();
        var store = new AfterburnerFileStore(installation.Directory, AfterburnerFileStore.DefaultBackupRoot, AfterburnerFileStore.LegacyBackupDirectoryFor(installation.Directory));
        var before = store.Read();
        var inspection = AfterburnerConfiguration.Inspect(before.Files[0].Before, before.Files[1].Before);
        var plan = AfterburnerConfiguration.Plan(before.Installation, before.Files[0].Before, before.Files[1].Before,
            inspection.Settings with { MaxCurrent = Math.Round(inspection.Settings.MaxCurrent + 0.1, 1, MidpointRounding.AwayFromZero) });
        var decision = AfterburnerDiffWindow.ShowAsync(plan, runtime.Preferences);
        await Task.Delay(400);
        var diff = AfterburnerDiffWindow.CurrentPreview ?? throw new InvalidOperationException("The debug diff did not open.");
        await SmokeTestRunner.CaptureElementAsync(diff.ThemeRoot, Path.Combine(output, "afterburner-debug-diff.png"));
        diff.CancelPreview();
        if (await decision)
        {
            throw new InvalidOperationException("Cancelling a debug diff must not approve writing.");
        }

        if (store.Read().Id != before.Id)
        {
            throw new InvalidOperationException("Read-only inspection and cancelling the preview changed Afterburner's configuration.");
        }

        foreach (var icon in new[] { "settings", "application", "terminal" })
        {
            if (Controls.WidgetIcon.LoadFailures.TryGetValue(icon, out var error))
            {
                throw new InvalidOperationException("Configuration icon failed to load: " + icon + ": " + error);
            }
        }

        var numericInput = VisualTreeQuery.Descendants<TextBox>(sharedCurrent).First();
        numericInput.Focus(FocusState.Programmatic);
        numericInput.Text = sharedCurrent.NumberFormatter.FormatDouble(savedCurrent + 0.1);
        Platform.Interop.NativeMethods.PostMessageW(
            WinRT.Interop.WindowNative.GetWindowHandle(integrationWindow), 0x0010, 0, 0);
        var finalCloseDialog = await FindPendingDialogAsync(integrationWindow);
        InvokeDialogButton(finalCloseDialog, "PrimaryButton");
        await Task.Delay(250);
        if (runtime.AfterburnerWindow is not null)
        {
            throw new InvalidOperationException("Exit without applying must close the window.");
        }

        if (store.Read().Id != before.Id)
        {
            throw new InvalidOperationException("Discarding pending changes must not write to the Afterburner files.");
        }
        await File.WriteAllTextAsync(Path.Combine(output, "verification.txt"),
            "PASS: Warning/info spacing, backup multiselect without a check column, readable launcher icon and executable-relative backup storage verified.  adjacent checkbox info, content-fitted initial height, fixed six-row list, multi-selection/restore gating, balanced margins and header columns, six-row backup limit, Go back/Exit without applying confirmation paths, shared/connector dirty detection and no unsaved label verified. Three content-sized panels, configuration/connector editing on the left, two-column backups and compact actions on the right, no page scrolling at default size, and independent editable connector timeouts verified. Dedicated integration window, gated MSI header launcher, independent connector tables and retained connector drafts verified. Software alarm checkbox, Explorer link, status badges, bottom feedback and Restart unknown-state guard verified without executing it. MSI integration retained one decimal place after repeated increments and typed values. Debug diff opened. Cancelling the preview left both installed Afterburner configuration files byte-for-byte unchanged. No elevated operation or alarm action was executed.");
    }
    private static async Task<ContentDialog> FindPendingDialogAsync(AfterburnerSettingsWindow window)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var dialog = VisualTreeHelper.GetOpenPopupsForXamlRoot(window.ThemeRoot.XamlRoot)
                .SelectMany(popup => popup.Child is ContentDialog direct ? new[] { direct }
                    : VisualTreeQuery.Descendants<ContentDialog>(popup.Child))
                .FirstOrDefault(candidate => candidate.Title?.ToString() == "Unapplied changes" && candidate.IsLoaded);
            if (dialog is not null)
            {
                await Task.Delay(150);
                return dialog;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException("Closing with pending edits did not show the confirmation dialog.");
    }

    private static void InvokeDialogButton(ContentDialog dialog, string name)
    {
        var button = VisualTreeQuery.Descendants<Button>(dialog).Single(candidate => candidate.Name == name);
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
    }
}