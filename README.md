# AI File Explorer

A cross-platform desktop file explorer with an embedded Claude AI assistant. Browse your file
system, run natural-language searches ("large PDFs from last month in Downloads"), chat with
Claude about what you're looking at, and ask it to summarise any text or code file — all without
leaving the app.

Built with .NET 10, Avalonia 12, and the Anthropic C# SDK.

---

## Features

- **Directory browser** — two-pane layout with a tree on the left and a sortable file list on the right
- **File operations** — copy, cut, paste, rename, delete, open with default app
- **NL search** — type a plain-English query; Claude extracts structured filters and walks your file system
  - 5-second timeout with automatic fallback to filename-substring search
  - Exponential back-off on rate-limit errors (up to 3 attempts)
  - Skips build-artifact directories (`node_modules`, `.git`, `bin`, `Library`, …)
- **AI chat sidebar** — multi-turn conversation with full context: current directory, selected file, and search results are baked into every system prompt
- **File summarisation** — select any text or code file and click **Summarize**; Claude streams a summary into the chat panel (files over 100 KB are truncated with a warning)

---

## Prerequisites

| Requirement | Version |
|---|---|
| .NET SDK | 10.0 or later |
| Anthropic API key | Any tier (free tier works) |

---

## API key setup

The app reads `ANTHROPIC_API_KEY` from the environment. There are two ways to provide it:

**Option A — `.env` file (recommended for development)**

Create a file named `.env` in the repository root (next to `AIFileExplorer.slnx`):

```
ANTHROPIC_API_KEY=sk-ant-api03-...
```

The app loads this file at startup via `Program.LoadEnvFile()`. The `.env` file is listed in
`.gitignore` and will not be committed.

**Option B — shell environment variable**

```bash
export ANTHROPIC_API_KEY=sk-ant-api03-...
dotnet run --project AIFileExplorer
```

---

## Build and run

```bash
# Clone and enter the repo
git clone <repo-url>
cd ai-file-explorer

# Run in development mode (loads .env automatically)
dotnet run --project AIFileExplorer

# Build a Release binary
dotnet build -c Release AIFileExplorer
```

The first run restores NuGet packages automatically.

### Run the tests

```bash
dotnet test AIFileExplorer.Tests
```

---

## Project structure

```
AIFileExplorer/
├── Models/          Plain data classes (FileSystemEntry, SearchFilter, ChatMessage, …)
├── Services/        All I/O and API calls (FileSystemService, NlSearchService, ChatService, …)
├── ViewModels/      MVVM view models (MainWindowViewModel, ChatViewModel, …)
├── Views/           Dialog windows (ConfirmDialog, RenameDialog)
├── MainWindow.axaml Main window layout
└── Program.cs       Entry point — loads .env, launches Avalonia
AIFileExplorer.Tests/
└── Unit tests for services and models (no network, no UI)
```

---

## Windows recompile note

The app targets `net10.0` and runs on macOS, Linux, and Windows as-is via Avalonia.

If you want a **native WPF build** (Windows only, ~2–4 hours of porting work):

### 1. Project file

```xml
<!-- Replace the Avalonia SDK with the Windows desktop SDK -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <!-- Remove all Avalonia PackageReferences; CommunityToolkit.Mvvm and Anthropic stay -->
</Project>
```

### 2. XAML namespace

Every `.axaml` file (rename to `.xaml`) starts with Avalonia's namespace:

```xml
xmlns="https://github.com/avaloniaui"
```

Replace with the WPF namespace:

```xml
xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
```

Also remove all `x:DataType="..."` attributes — WPF uses reflection-based bindings.

### 3. Avalonia-specific APIs to replace

| Avalonia | WPF equivalent |
|---|---|
| `Dispatcher.UIThread.Post(action, DispatcherPriority.Background)` | `Application.Current.Dispatcher.BeginInvoke(action, DispatcherPriority.Background)` |
| `Dispatcher.UIThread.InvokeAsync(action)` | `Application.Current.Dispatcher.InvokeAsync(action)` |
| `TappedEventArgs` / `DoubleTapped` | `MouseButtonEventArgs` / `MouseDoubleClick` |
| `AppBuilder.Configure<App>().UsePlatformDetect()…` | Remove `Program.cs`; WPF uses `App.xaml` `StartupUri` |
| `AvaloniaXamlLoader.Load(this)` in `App.axaml.cs` | Remove; WPF loads XAML automatically |

### 4. Controls that map 1-to-1

`Grid`, `GridSplitter`, `ScrollViewer`, `ListBox`, `TreeView`, `HierarchicalDataTemplate`,
`ItemsControl`, `Border`, `TextBlock`, `TextBox`, `Button`, `ProgressBar`, `DockPanel`,
`StackPanel`, `ContextMenu`, `MenuItem`, `Separator` — all exist in WPF with the same names
and nearly identical attribute sets.

### 5. What does not need to change

- All `Models/` classes
- All `Services/` classes (pure .NET, no UI dependencies)
- `CommunityToolkit.Mvvm` — cross-platform, works identically in WPF
- The Anthropic SDK — cross-platform
- `MainWindowViewModel`, `ChatViewModel`, `FileSystemViewModel`, `DirectoryNodeViewModel`

The only files that require editing are `Program.cs`, `App.axaml(.cs)`, `MainWindow.axaml(.cs)`,
and the two dialog Views.
