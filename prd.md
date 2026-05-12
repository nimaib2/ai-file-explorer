# PRD: FileAssistant

**Status:** Draft v1
**Author:** [you]
**Last updated:** 2026-05-11
**Timeframe:** 4 weeks, ~60 hours of solo work

> This is a learning project. The product goals below are real, but the
> *meta-goal* is to learn C#/.NET well enough to be effective on day one of a
> Windows Experiences internship. When product goals and learning goals
> conflict, learning goals win.

---

## 1. Problem

Files accumulate faster than people organize them. The Downloads folder, the
Desktop, and the catch-all "stuff" directory become write-only — users know
something is in there but can't find it because:

- They don't remember the filename.
- They remember the *content* or *context*, not the file metadata.
- Windows Search indexes filenames and some content, but doesn't understand
  semantic queries like "the resume I edited in March" or "notes from the
  conversation about pricing."

The user has to either remember exact keywords or browse manually.

## 2. Users

A single user persona for v1: **a developer or power user on Windows** with a
few thousand files in a working directory, comfortable with a CLI, and willing
to provide their own LLM API key.

Explicitly **not** in scope as users:
- Non-technical users (no GUI in v1).
- Enterprise users (no compliance, no shared indexes, no SSO).
- Users on macOS or Linux (the project targets Windows, though the code will
  happen to be cross-platform).

## 3. Goals (and non-goals)

### Product goals
1. Index a chosen directory tree and let the user search it by natural-language
   query.
2. Answer questions *about* the indexed files using retrieval-augmented
   generation (RAG).
3. Run entirely locally except for LLM/embedding API calls.

### Learning goals
1. Become fluent in idiomatic C# (records, LINQ, async/await, nullable
   reference types).
2. Build muscle memory for the modern .NET app skeleton: `Microsoft.Extensions.*`
   for DI, configuration, and logging.
3. Get hands-on with EF Core, `System.CommandLine`, xUnit, and at least one
   resilience library (Polly).
4. Practice writing a design doc and PR-quality commits.

### Non-goals (the cut list — this is the most important section)
- **No GUI in v1.** WPF/WinUI is a separate, much larger project; bolting one
  on in week 4 trades depth for a screenshot.
- **No shell integration.** No context menu, no IFileOperation, no thumbnail
  providers. These are what a real File Explorer needs, and they would consume
  the entire four weeks.
- **No file modifications in v1.** The tool reads. It does not move, rename,
  or delete. An `organize` command that *proposes* operations is a stretch
  goal.
- **No real-time index updates.** Re-running `index` picks up changes. No
  `FileSystemWatcher` in v1.
- **No multi-user, no sharing, no cloud sync.** The index is a local SQLite
  file.
- **No PDF or Office-document extraction in v1.** Plain text only
  (`.txt`, `.md`, `.csv`). PDF via PdfPig is a stretch goal.
- **No fine-tuned embeddings, no local models, no vector DB.** A flat
  cosine-similarity scan over a `BLOB` column is fine for v1's expected scale.
- **No prompt-injection defense beyond basic awareness.** Documented as a
  known risk; not a v1 mitigation target.

If a request would expand any of the above, the answer in v1 is no.

## 4. User stories

In priority order. Anything below the cut line is v2.

1. **As a user, I can index a directory** so the tool knows what files exist.
   `fa index C:\Users\me\Documents`
2. **As a user, I can re-index incrementally** without re-processing unchanged
   files. Subsequent `fa index` runs are fast.
3. **As a user, I can search semantically** for files matching a description.
   `fa search "notes about the database migration"` returns ranked file paths.
4. **As a user, I can ask questions about my files** and get answers grounded
   in their content. `fa chat "what did I decide about the database
   migration?"` returns an LLM response citing the files it used.
5. **As a user, I can see indexing status and costs**. `fa status` shows row
   counts and approximate API spend.

--- cut line ---

6. **As a user, I get proposed organize actions** I can approve. (Stretch)
7. **As a user, I can index PDFs.** (Stretch)
8. **As a user, I have a GUI.** (v2, separate project)

## 5. Functional requirements

| ID | Requirement | Priority |
|----|-------------|----------|
| F1 | `index <path>` walks the directory recursively and persists file metadata to a local SQLite database. | Must |
| F2 | Re-running `index <path>` skips files whose `(path, modified_utc, size)` is unchanged. | Must |
| F3 | Extracts text content from `.txt`, `.md`, `.csv` files; stores up to N characters per file. | Must |
| F4 | Generates one embedding per file (over filename + content prefix) and stores it as a `BLOB`. | Must |
| F5 | `search <query>` returns top-K files by cosine similarity, with scores. | Must |
| F6 | `chat <query>` performs RAG: retrieves top-K files, sends content + question to an LLM, prints the response. | Must |
| F7 | `status` prints indexed file count, embedded file count, DB size, and approximate API spend this session. | Must |
| F8 | All commands respect `Ctrl+C` and cancel cleanly without DB corruption. | Must |
| F9 | API calls retry on transient failures (429, 5xx) with exponential backoff. | Should |
| F10 | API key is read from user secrets, not source code. | Must |
| F11 | The tool never writes to indexed files. | Must |

## 6. Non-functional requirements

- **Performance:** Indexing 10,000 plain-text files of typical size completes
  in under 10 minutes on a developer laptop, bounded mostly by embedding API
  latency. Search returns in under 500ms.
- **Reliability:** Indexing survives unreadable files, permission errors, and
  locked files. One bad file does not abort the run.
- **Testability:** Core library has unit and integration tests against a temp
  directory. CI runs them on every push.
- **Code quality:** Nullable reference types enabled. No suppressed warnings
  without a comment explaining why.

## 7. Open questions

These are real uncertainties, not rhetorical questions. They will be resolved
during the build and the resolution will be documented in the design doc.

1. **Which embedding provider?** Anthropic doesn't offer embeddings directly;
   OpenAI's `text-embedding-3-small` is the obvious cheap default. Mixing
   providers (Anthropic for chat, OpenAI for embeddings) is fine but adds a
   second API key.
2. **Embedding granularity: per-file or per-chunk?** Per-file is simpler and
   the v1 default. Per-chunk gives better recall on long files but multiplies
   storage and API cost. Defer to v2 unless v1 results are visibly poor.
3. **Truncation length for embedded text.** First 2000 characters is a guess.
   May revisit after seeing real results.
4. **Where does the SQLite file live?** `%LOCALAPPDATA%\FileAssistant\index.db`
   is the right answer for a real Windows app. For v1, a `--db` flag with a
   sensible default is fine.

## 8. Success criteria

The project is "done" when:

- All Must-have functional requirements are implemented and tested.
- The repo has a README that lets a stranger clone, build, and run it.
- The repo has a design doc explaining decisions and tradeoffs.
- CI is green on `main`.
- A 2–3 minute demo video exists.
- The author can, without notes, explain how any part of the code works in
  an interview setting.

The last bullet is the most important one and the easiest to fail. If at the
end of week 4 there is any file in the repo whose contents are mysterious to
the author, the project has failed its primary goal regardless of feature
completeness.
