using System.Threading.Tasks;

namespace AIFileExplorer.Services;

/// <summary>
/// Abstracts UI dialogs so the ViewModel can request them without
/// importing Avalonia or knowing which Window type to create.
///
/// The ViewModel depends on this interface; the concrete implementation
/// lives in MainWindow.axaml.cs and is injected at construction time.
/// This keeps Avalonia Window types out of the ViewModel layer entirely.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a yes/no confirmation for a destructive delete.</summary>
    Task<bool> ConfirmDeleteAsync(string name);

    /// <summary>Shows an input prompt pre-filled with the current name.</summary>
    Task<string?> PromptRenameAsync(string currentName);
}
