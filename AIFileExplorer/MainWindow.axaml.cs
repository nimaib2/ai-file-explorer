using Avalonia.Controls;
using AIFileExplorer.ViewModels;

namespace AIFileExplorer;

/// <summary>
/// Code-behind for MainWindow.
///
/// Now contains only infrastructure: InitializeComponent() and DataContext
/// assignment. All logic lives in MainWindowViewModel; all layout lives in
/// MainWindow.axaml. The Click handler is gone — the button binds directly
/// to the generated ScanCommand.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
