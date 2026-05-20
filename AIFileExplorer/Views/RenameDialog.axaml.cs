using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIFileExplorer.Views;

/// <summary>
/// Input dialog for renaming a file or directory. Returns the new name
/// string via ShowDialog&lt;string?&gt;, or null if the user cancels.
/// </summary>
public partial class RenameDialog : Window
{
    public RenameDialog() => InitializeComponent();

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        // Select all so the user can immediately type a replacement name
        // without manually clearing the field first.
        NameBox.SelectAll();
    }

    private void OnRename(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(name))
            Close(name);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
