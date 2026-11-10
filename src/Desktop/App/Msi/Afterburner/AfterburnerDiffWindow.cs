#if DEBUG || TELEMETRY_DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using RTXLens.App.Services;
using RTXLens.Presentation.Msi.Afterburner;
using RTXLens.Presentation.Settings;

namespace RTXLens.App.Msi.Afterburner;

internal sealed class AfterburnerDiffWindow : Window
{
    private readonly TaskCompletionSource<bool> _decision = new();
    internal static AfterburnerDiffWindow? CurrentPreview { get; private set; }
    internal FrameworkElement ThemeRoot => (FrameworkElement)Content;
    internal void CancelPreview() => Finish(false);

    private AfterburnerDiffWindow(AfterburnerChangeSet plan, AppPreferences preferences)
    {
        Title = "RTX Lens - Afterburner configuration diff";
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));
        var themeName = preferences.Theme == AppTheme.Dark || preferences.Theme == AppTheme.System
            && Application.Current.RequestedTheme == ApplicationTheme.Dark ? "Dark" : "Light";
        var palette = Application.Current.Resources.MergedDictionaries
            .Where(dictionary => dictionary.ThemeDictionaries.ContainsKey(themeName))
            .Select(dictionary => (ResourceDictionary)dictionary.ThemeDictionaries[themeName]).Last();
        var foreground = (Brush)palette["MonitorForeground"];
        var root = new Grid { Padding = new Thickness(20), RowSpacing = 12, Background = (Brush)palette["MonitorBackground"] };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(new TextBlock
        {
            Text = "Inspect the actual files before writing. Red lines are removed; green lines are added. No files have been changed.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground
        });
        var tabs = new TabView { IsAddTabButtonVisible = false, CanDragTabs = false, CanReorderTabs = false };
        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        foreach (var file in plan.Files)
        {
            var before = IniDocument.Preview(file.Before);
            var after = IniDocument.Preview(file.After);
            var stack = new StackPanel { Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            stack.Children.Add(new TextBlock
            {
                Text = Path.Combine(plan.Installation, file.RelativePath)
                    + "\nEncoding: " + before.EncodingName + " -> " + after.EncodingName
                    + " | Line endings: " + LineEndings(before.Text) + " -> " + LineEndings(after.Text)
                    + (file.BeforeExists ? "" : " | Missing file - will be restored from backup."),
                TextWrapping = TextWrapping.Wrap,
                Foreground = foreground
            });
            var text = new RichTextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 13, IsTextSelectionEnabled = true, Foreground = foreground };
            AddDiff(text, before.Text, after.Text);
            stack.Children.Add(text);
            tabs.TabItems.Add(new TabViewItem
            {
                Header = Path.GetFileName(file.RelativePath) + (file.Changed ? " - modified" : " - unchanged"),
                IsClosable = false,
                Content = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }
            });
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10 };
        var cancel = new Button { Content = "Cancel" };
        var apply = new Button { Content = "Apply changes" };
        foreach (var button in new[] { cancel, apply })
        {
            button.Background = (Brush)palette["MonitorTrack"];
            button.Foreground = foreground;
            button.BorderBrush = (Brush)palette["MonitorBorder"];
        }
        cancel.Click += (_, _) => Finish(false);
        apply.Click += (_, _) => Finish(true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        Content = root;
        ThemeService.Apply(root, preferences);
        Closed += (_, _) =>
        {
            _decision.TrySetResult(false);
            if (ReferenceEquals(CurrentPreview, this))
            {
                CurrentPreview = null;
            }
        };
    }

    internal static Task<bool> ShowAsync(AfterburnerChangeSet plan, AppRuntime runtime)
    {
        var window = new AfterburnerDiffWindow(plan, runtime.Preferences);
        runtime.TrackWindow(window, "afterburner-preview", 1100, 760);
        CurrentPreview = window;
        window.Activate();
        return window._decision.Task;
    }

    private void Finish(bool accepted)
    {
        _decision.TrySetResult(accepted);
        Close();
    }

    private static string LineEndings(string text)
    {
        var kinds = new List<string>();
        if (text.Contains("\r\n", StringComparison.Ordinal))
        {
            kinds.Add("CRLF");
        }

        var remaining = text.Replace("\r\n", "", StringComparison.Ordinal);
        if (remaining.Contains('\n'))
        {
            kinds.Add("LF");
        }

        if (remaining.Contains('\r'))
        {
            kinds.Add("CR");
        }

        return kinds.Count == 0 ? "none" : string.Join("/", kinds);
    }

    private static void AddDiff(RichTextBlock target, string before, string after)
    {
        var original = before.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var proposed = after.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        // Bound the LCS table; unusually large files still show a complete, conservative before/after diff.
        if ((long)original.Length * proposed.Length > 2_000_000)
        {
            foreach (var line in original)
            {
                AddLine("- " + line, true);
            }

            foreach (var line in proposed)
            {
                AddLine("+ " + line, false);
            }

            return;
        }

        var lengths = new int[original.Length + 1, proposed.Length + 1];
        for (var i = original.Length - 1; i >= 0; i--)
        {
            for (var j = proposed.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = original[i] == proposed[j] ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var left = 0;
        var right = 0;
        while (left < original.Length || right < proposed.Length)
        {
            if (left < original.Length && right < proposed.Length && original[left] == proposed[right])
            {
                AddLine("  " + original[left++], null);
                right++;
            }
            else if (right < proposed.Length && (left == original.Length || lengths[left, right + 1] > lengths[left + 1, right]))
            {
                AddLine("+ " + proposed[right++], false);
            }
            else
            {
                AddLine("- " + original[left++], true);
            }
        }

        void AddLine(string line, bool? removed)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0) };
            var run = new Run { Text = line.Length == 0 ? " " : line };
            if (removed is { } isRemoved)
            {
                run.Foreground = new SolidColorBrush(isRemoved ? global::Windows.UI.Color.FromArgb(255, 255, 120, 120)
                    : global::Windows.UI.Color.FromArgb(255, 100, 235, 165));
            }

            paragraph.Inlines.Add(run);
            target.Blocks.Add(paragraph);
        }
    }
}
#endif