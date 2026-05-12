# AI File Explorer — 4-Week Daily Plan

## Context
Nima is a Java/OOP developer (no prior C#/.NET) building an AI-powered Windows File Explorer to learn C# and prepare for a Microsoft Windows Experiences internship starting ~2026-06-08. The project doubles as a learning vehicle: each week's features introduce a new layer of the stack.

**Stack:** C# 12 / .NET 8 · WPF (XAML + MVVM) · CommunityToolkit.Mvvm · Claude API (Anthropic .NET SDK) · System.IO

**Priority AI features:** Natural language file search (primary) + sidebar chat assistant (secondary)

---

## Weekly Milestones

| Week | Goal | Deliverable |
|------|------|-------------|
| 1 | C# fluency + project scaffolding | Compiling WPF shell, no crashes |
| 2 | Core file explorer | Browse, navigate, basic search, file ops |
| 3 | AI natural language search | "Find my resume from last month" works end-to-end |
| 4 | Chat assistant + polish | Chat sidebar, file summarization, README |

---

## Week 1 — C# Fundamentals + Project Setup

**Goal:** Get comfortable enough with C# that Java muscle memory stops causing bugs. Stand up the project.

**Day 1 — Environment + C# syntax**
- Install .NET 8 SDK, VSCode with C# Dev Kit extension, verify `dotnet --version`
- Read: [C# for Java Developers cheatsheet](https://learn.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/) — focus on properties vs getters/setters, `var`, string interpolation, nullability (`?`, `??`, `!`)
- Exercise: Port a small Java class (e.g., a linked list node) to C# by hand

**Day 2 — Collections + LINQ**
- C# generics, `List<T>`, `Dictionary<K,V>`, `IEnumerable<T>`
- LINQ = Java Streams: `Where`, `Select`, `OrderBy`, `FirstOrDefault`, `ToList`
- Exercise: Write LINQ queries over a hardcoded list of file records

**Day 3 — async/await + Tasks**
- `Task` = `CompletableFuture`, `async`/`await` syntax
- `Task.Run`, `CancellationToken`, why UI work must stay on the UI thread
- Exercise: Fetch a URL asynchronously and print the response length

**Day 4 — .NET project structure + NuGet**
- Solution (`.sln`) vs Project (`.csproj`), namespaces, `using` directives
- `dotnet new wpf -n AIFileExplorer` — create the project
- Install first NuGet package: `dotnet add package CommunityToolkit.Mvvm`
- Read: What is CommunityToolkit.Mvvm and why it makes WPF much easier

**Day 5 — System.IO file APIs**
- `DirectoryInfo`, `FileInfo`, `Directory.GetFiles`, `File.ReadAllText`
- Recursive directory walk, file metadata (size, modified date, extension)
- Exercise: Console app that prints all files >1MB under `C:\Users\YourName\Documents`

**Day 6 — WPF + XAML intro**
- XAML as XML-based UI markup; contrast with HTML
- `Window`, `Grid`, `StackPanel`, `Button`, `TextBox`, `ListView`, `TreeView`
- Modify the default WPF window: add a toolbar with a text box and a button

**Day 7 — Data Binding + MVVM pattern**
- MVVM: Model (data) · ViewModel (logic) · View (XAML) — no logic in code-behind
- `INotifyPropertyChanged`, `ObservableProperty` attribute (CommunityToolkit shortcut)
- `RelayCommand` for button clicks
- Exercise: Bind a `ListView` to an `ObservableCollection<string>` and add items via a button

---

## Week 2 — Core File Explorer

**Goal:** A functional file browser with no AI — proves the foundation before adding complexity.

**Day 8 — Main window layout**
- Two-pane layout: `TreeView` (left, directory tree) + `ListView` (right, file list)
- `GridSplitter` between panes so user can resize
- Top toolbar: address bar (`TextBox`) + search box
- Status bar at bottom (file count, selected item info)

**Day 9 — Directory tree (TreeView)**
- `FileSystemViewModel` with `ObservableCollection<DirectoryNodeViewModel>`
- Lazy-load children on expand (don't walk the whole drive at startup)
- Show drive letters as root nodes, folders only (files go in the right pane)

**Day 10 — File list (ListView) + navigation**
- Clicking a tree node populates `ListView` with files + subfolders
- Columns: Name, Size, Type (extension), Date Modified
- Double-click folder → navigate into it; double-click file → `Process.Start` to open it
- Address bar updates on navigation; typing a path and pressing Enter navigates

**Day 11 — File operations**
- Right-click context menu: Copy, Cut, Paste, Delete, Rename, Open
- Use `File.Copy`, `File.Move`, `File.Delete`, `Directory.Move`
- Confirmation dialogs for destructive actions (`MessageBox`)
- Keyboard shortcuts: F2 (rename), Delete (delete), Ctrl+C/X/V

**Day 12 — Basic text search**
- Search box filters the current directory's file list in real-time
- Case-insensitive substring match on filename
- Show result count in status bar

**Day 13 — UX polish pass**
- File type icons (use `Shell32` or extension-to-icon mapping)
- Sort columns by clicking header
- "Up" button / backspace to navigate to parent folder
- Handle access-denied exceptions gracefully (show a friendly message, don't crash)

**Day 14 — Refactor to clean MVVM**
- Move any remaining logic out of code-behind into ViewModels
- Unit test the file-listing logic (xUnit: `dotnet add package xunit`)
- Milestone check: can browse the full file system, open files, perform basic operations

---

## Week 3 — Claude API + Natural Language Search

**Goal:** The headline AI feature — describe what you want in English, get results from the real file system.

**Day 15 — Anthropic SDK setup + first call**
- `dotnet add package Anthropic` (official .NET SDK)
- Store API key in environment variable (`ANTHROPIC_API_KEY`), never hardcode
- Make a "Hello Claude" call from a service class, print the response to the debug console

**Day 16 — NL search prompt design**
- System prompt teaches Claude to parse a natural language query into a structured filter:
  ```
  Extract: file type, name keywords, date range, size range, location hint.
  Return JSON only.
  ```
- Write unit tests for the JSON parser (covers edge cases like missing fields)

**Day 17 — Wire NL search to System.IO**
- `NaturalLanguageSearchService`: sends query to Claude → parses JSON → builds `FileQuery` object
- `FileSearchEngine`: walks directory tree applying `FileQuery` filters (extension, date, size, name)
- Show spinner in UI while the async search runs

**Day 18 — Handle complex queries**
- Date ranges: "last month", "before 2024", "this week" → compute actual `DateTime` bounds
- Size: "large files" → >50MB; "small" → <1MB; explicit "bigger than 10MB"
- Location hints: "in Documents", "on Desktop" → scope the search root

**Day 19 — Results UI**
- Dedicated "Search Results" view (separate from normal file list)
- Show: file path, size, date, relevance snippet (why this file matched)
- Click result → navigate the tree to that file's parent folder and highlight it

**Day 20 — Robustness**
- `CancellationToken` so new search cancels in-flight old search
- Rate limit handling: exponential backoff if Claude returns 429
- Timeout fallback: if Claude takes >5s, fall back to plain filename substring search

**Day 21 — End-to-end testing + polish**
- Test 10 realistic queries against your actual file system
- Refine the prompt based on failures
- Milestone check: "find my resume from last year" and "show me videos bigger than 500MB" both return correct results

---

## Week 4 — Chat Assistant Sidebar + Final Polish

**Goal:** Add the chat sidebar, file summarization, and ship a complete documented project.

**Day 22 — Chat sidebar UI**
- Collapsible right-side panel (`GridSplitter` + `Visibility` binding)
- Message bubbles: user messages right-aligned, Claude responses left-aligned
- `ScrollViewer` that auto-scrolls to latest message
- Input box + Send button at the bottom

**Day 23 — Chat backend + streaming**
- Maintain `List<ChatMessage>` conversation history (multi-turn context)
- Use Claude streaming API so responses appear word-by-word (better UX)
- Display a "Claude is thinking…" indicator while waiting for first token

**Day 24 — Context-aware chat**
- Selected file(s) in the explorer automatically become context: "You have selected: resume.pdf (245KB, modified Jan 2025)"
- Current directory is also passed as context
- User can ask: "What kind of files are in this folder?" and Claude knows

**Day 25 — File summarization**
- When a text/PDF/code file is selected, "Summarize" button appears in toolbar
- Read file content (`File.ReadAllText`), send to Claude with "Summarize this file" prompt
- Display summary in the chat panel
- Guard: skip binary files; truncate files >100KB with a warning

**Day 26 — Integration testing + bug fixes**
- Full walkthrough: launch → browse → NL search → view results → select file → chat about it → summarize
- Fix any crashes, UI jank, or bad Claude responses found during walkthrough
- Test on a folder you haven't tested before (e.g., `C:\Program Files` — handle access-denied)

**Day 27 — Code cleanup + documentation**
- Write `README.md`: what the app does, how to build it, how to set the API key
- Add XML doc comments to all public classes and methods
- Remove dead code, ensure consistent naming conventions (C# uses PascalCase for everything public)

**Day 28 — Final review + stretch goals**
- Record a short screen capture demo of the app working end-to-end
- Reflection: write 3 things learned about C#/.NET that surprised you vs Java
- Stretch goals (if time allows): drag-and-drop between panes, file tagging, dark mode

---

## Project Structure

```
AIFileExplorer/
├── AIFileExplorer.sln
└── AIFileExplorer/
    ├── Models/           # FileItem, FileQuery, ChatMessage
    ├── ViewModels/       # MainWindowViewModel, SearchResultsViewModel, ChatViewModel
    ├── Views/            # MainWindow.xaml, ChatPanel.xaml, SearchResultsView.xaml
    ├── Services/         # ClaudeService, FileSearchEngine, NaturalLanguageSearchService
    ├── Helpers/          # FileIconHelper, FileSizeFormatter
    └── Tests/            # xUnit test project (separate .csproj)
```

## Key C#/.NET Concepts to Master (Java Mappings)

| Java | C# equivalent |
|------|--------------|
| `interface` | `interface` (same) |
| `abstract class` | `abstract class` (same) |
| getter/setter methods | `{ get; set; }` properties |
| `CompletableFuture<T>` | `Task<T>` with `async`/`await` |
| Stream API (`filter`, `map`) | LINQ (`Where`, `Select`) |
| `ArrayList<T>` | `List<T>` |
| `HashMap<K,V>` | `Dictionary<K,V>` |
| `Optional<T>` | nullable reference types (`T?`) |
| `synchronized` | `lock`, `SemaphoreSlim` |

## Verification

End-to-end test at end of each week:
- **Week 1:** `dotnet run` launches a blank WPF window with a button that responds to clicks
- **Week 2:** Can browse from C:\ to any subfolder, rename a file, and filter by name
- **Week 3:** Type "find PDFs modified this year" → results appear in <10 seconds
- **Week 4:** Select a `.txt` file → click Summarize → summary appears in chat panel
