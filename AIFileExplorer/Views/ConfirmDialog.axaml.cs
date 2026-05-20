using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIFileExplorer.Views;

/// <summary>
/// Yes/No confirmation dialog. Returns true (confirmed) or false (cancelled)
/// via ShowDialog&lt;bool&gt;. If the user closes the window without clicking
/// either button, ShowDialog returns the bool default (false = cancelled).
/// </summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    public ConfirmDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e)  => Close(false);
}
