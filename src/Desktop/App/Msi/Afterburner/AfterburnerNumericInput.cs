using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Globalization.NumberFormatting;

namespace Rtx5090Monitoring.App.Msi.Afterburner;

internal static class AfterburnerNumericInput
{
    internal static bool Commit(NumberBox field)
    {
        if (FindInput(field) is not { } input)
        {
            return true; // An unopened connector tab has no rendered input; its Value already holds the draft.
        }

        if (string.IsNullOrWhiteSpace(input.Text))
        {
            field.Value = double.NaN;
            return true;
        }

        if (field.NumberFormatter is INumberParser parser && parser.ParseDouble(input.Text) is { } value
            && double.IsFinite(value))
        {
            field.Value = value;
            return true;
        }

        return false;
    }

    private static TextBox? FindInput(DependencyObject parent)
    {
        if (parent is TextBox input)
        {
            return input;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            if (FindInput(VisualTreeHelper.GetChild(parent, index)) is { } child)
            {
                return child;
            }
        }

        return null;
    }
}