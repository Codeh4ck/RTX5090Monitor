using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RTXLens.App.Services;
using RTXLens.Contracts.Updates;
using RTXLens.Presentation.Settings;
using RTXLens.Presentation.Updates;

namespace RTXLens.App.Updates;

internal sealed partial class ReleaseNotesWindow : Window
{
    private string? _changelog;

    public ReleaseNotesWindow(AppPreferences preferences)
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "monitor.ico"));

        ThemeService.Apply(Root, preferences);
        Root.ActualThemeChanged += (_, _) => RenderMarkdown();
    }

    public void Loading(UpdateNotice? update) =>
        Show(update?.ReleaseTag, update?.ReleaseUrl, "Loading release notes…");

    public void Display(ReleaseNotes notes, UpdateNotice? update)
    {
        var published = "Published " + notes.PublishedAt.ToLocalTime().ToString("g");
        var details = update is null ? published : update.Description + Environment.NewLine + published;
        Show(notes.ReleaseTag, notes.ReleaseUrl, details, notes.Changelog);
    }

    public void Unavailable(UpdateNotice? update, bool differentRelease) =>
        Show(update?.ReleaseTag, update?.ReleaseUrl, differentRelease
            ? "Release notes for this version are not available yet."
            : "Could not load release notes. Reopen this window to try again.");

    private void Show(string? tag, string? releaseUrl, string status, string? changelog = null)
    {
        VersionText.Text = tag is null ? "Release notes" : "RTX Lens " + tag;
        StatusText.Text = status;
        DownloadLink.Visibility = ReleaseLink.Visibility = releaseUrl is null ? Visibility.Collapsed : Visibility.Visible;

        if (releaseUrl is not null)
        {
            ReleaseLink.NavigateUri = new Uri(releaseUrl);
            DownloadLink.NavigateUri = ReleaseNotes.DownloadFor(releaseUrl);
            ToolTipService.SetToolTip(DownloadLink, DownloadLink.NavigateUri.AbsoluteUri);
        }

        _changelog = changelog;
        RenderMarkdown();
        NotesScroll.ChangeView(null, 0, null, true);
    }

    private void RenderMarkdown()
    {
        if (_changelog is null)
        {
            MarkdownBody.Children.Clear();
            return;
        }

        ReleaseNotesMarkdown.Render(_changelog, MarkdownBody, VersionText.Foreground, DownloadLink.Foreground);
    }

}
