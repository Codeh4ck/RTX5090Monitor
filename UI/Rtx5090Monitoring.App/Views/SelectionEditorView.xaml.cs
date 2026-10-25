using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Rtx5090Monitoring.Presentation.Configuration;

namespace Rtx5090Monitoring.App.Views;

public sealed partial class SelectionEditorView : UserControl
{
    private readonly Action<SelectionEditorViewModel> _delete;
    public SelectionEditorViewModel Model { get; }

    internal SelectionEditorView(SelectionEditorViewModel model, Action<SelectionEditorViewModel> delete)
    {
        Model = model;
        _delete = delete;
        InitializeComponent();
    }

    private void Add_Click(object sender, RoutedEventArgs args) => Model.AddSource();
    private void Up_Click(object sender, RoutedEventArgs args) => Model.MoveSource(-1);
    private void Down_Click(object sender, RoutedEventArgs args) => Model.MoveSource(1);
    private void Remove_Click(object sender, RoutedEventArgs args) => Model.RemoveSource();
    private void Delete_Click(object sender, RoutedEventArgs args) => _delete(Model);
}
