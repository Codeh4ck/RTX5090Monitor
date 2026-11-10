using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using XamlInline = Microsoft.UI.Xaml.Documents.Inline;

namespace RTXLens.App.Updates;

internal static class ReleaseNotesMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

    public static void Render(string markdown, StackPanel target, Brush foreground, Brush accent)
    {
        target.Children.Clear();
        AppendBlocks(Markdown.Parse(markdown, Pipeline), target, foreground, accent);
    }

    private static void AppendBlocks(ContainerBlock blocks, StackPanel target, Brush foreground, Brush accent)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ListBlock list:
                    var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;

                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var row = new Grid { ColumnSpacing = 8 };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition());
                        row.Children.Add(new TextBlock { Text = list.IsOrdered ? $"{number++}." : "•", MinWidth = 12, Foreground = foreground });

                        var body = new StackPanel { Spacing = 5 };
                        Grid.SetColumn(body, 1);
                        row.Children.Add(body);
                        AppendBlocks(item, body, foreground, accent);
                        target.Children.Add(row);
                    }
                    break;

                case CodeBlock code:
                    target.Children.Add(new TextBlock
                    {
                        Text = code.Lines.ToString(), FontFamily = new FontFamily("Consolas"),
                        TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Foreground = foreground
                    });
                    break;

                case LeafBlock { Inline: { } content } leaf:
                    var text = new RichTextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Foreground = foreground };
                    var paragraph = new Paragraph();

                    if (leaf is HeadingBlock heading)
                    {
                        paragraph.FontSize = heading.Level <= 2 ? 20 : 16;
                        paragraph.FontWeight = FontWeights.SemiBold;
                    }

                    AppendInlines(content, paragraph.Inlines, accent);
                    text.Blocks.Add(paragraph);
                    target.Children.Add(text);
                    break;

                case QuoteBlock quote:
                    var quoteBody = new StackPanel { Spacing = 5, Margin = new Thickness(16, 0, 0, 0) };
                    AppendBlocks(quote, quoteBody, foreground, accent);
                    target.Children.Add(quoteBody);
                    break;
            }
        }
    }

    private static void AppendInlines(ContainerInline container, InlineCollection target, Brush accent)
    {
        foreach (var inline in container)
        {
            var element = inline switch
            {
                LiteralInline literal => new Run { Text = literal.Content.ToString() },
                CodeInline code => new Run { Text = code.Content, FontFamily = new FontFamily("Consolas") },
                LineBreakInline { IsHard: true } => new LineBreak(),
                LineBreakInline => new Run { Text = " " },
                HtmlEntityInline entity => new Run { Text = entity.Transcoded.ToString() },
                AutolinkInline autoLink => Link(autoLink.Url, autoLink.Url, accent),
                EmphasisInline emphasis => emphasis.DelimiterCount == 2 ? new Bold() : new Italic(),
                LinkInline { IsImage: false } link when WebUri(link.Url) is { } uri => new Hyperlink { NavigateUri = uri, Foreground = accent },
                ContainerInline => new Span(),
                _ => null
            };

            if (element is Span span && inline is ContainerInline children)
            {
                AppendInlines(children, span.Inlines, accent);
            }

            if (element is not null)
            {
                target.Add(element);
            }
        }
    }

    private static XamlInline Link(string url, string label, Brush accent)
    {
        var text = new Run { Text = label };
        if (WebUri(url) is not { } uri)
        {
            return text;
        }

        var link = new Hyperlink { NavigateUri = uri, Foreground = accent };
        link.Inlines.Add(text);
        return link;
    }

    private static Uri? WebUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http" ? uri : null;
}
