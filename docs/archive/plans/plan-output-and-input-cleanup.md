# Plan: Output And Input Cleanup

Status: Validated
Scope: Analysis + implementation plan. No code changes in this plan.

## Problem Statement

This plan addresses these five issues:

1. HTTP metadata is mixed into JSON output.
2. `--xxx-file` vs `--xxx-adf-file` options are ambiguous.
3. No explicit `--plain` mode exists for human-readable output.
4. No explicit minified JSON mode exists.
5. BOM in file payloads can cause Jira `INVALID_INPUT`.

## Reconnaissance Summary

### HTTP execution and response handling

- `src/acjr3/Http/RequestExecution.cs`
  - `RequestCommandOptions` currently has `Raw` and `IncludeHeaders` only.
  - `HandleResponseAsync` always prints formatter output to stdout.
  - `ExecutePaginatedAsync` hardcodes `HTTP 200 OK` before body.
- `src/acjr3/Output/ResponseFormatter.cs`
  - `Format(...)` always prepends `HTTP <code> <reason>`.
  - `includeHeaders` only controls headers, not status line.
  - `raw` only toggles pretty JSON vs compact/raw text.

### Output rendering and formatting

- Output behavior is centralized in `ResponseFormatter`, but command option semantics are not expressive enough (format/style/metadata are coupled to `raw` + status line default).
- `docs/behavior.md` currently documents status-first output and `--raw` as “no pretty print”.

### CLI option definitions

- `src/acjr3/App/Program.cs` (`request` command): `--raw`, `--include-headers`.
- Jira shortcut commands (`src/acjr3/Commands/Jira/*.cs`) repeat `--raw` across many command handlers.
- `IssueCommands` exposes both generic and ADF-specific file flags:
  - `--description-adf-file`
  - `--field-adf-file`
  - `--body-adf-file`
  - plus generic `--body-file`

### File input/payload loading

- Payload reads are spread across:
  - `src/acjr3/App/Program.cs` (`ReadAllTextAsync` for `--body-file`)
  - `src/acjr3/Commands/Jira/IssueCommands.cs` (`TryResolveBody`, `TryReadJsonObjectFile`)
  - `src/acjr3/Commands/Jira/IssueLinkCommands.cs` (`TryResolveBody`)
  - `src/acjr3/Commands/Jira/SearchCommands.cs` (`--jql-file`)
- No centralized BOM normalization utility exists.

## Proposed Coherent Output Model

Define output as 3 orthogonal dimensions:

1. `format`: `json` or `plain`
2. `decoration`: include HTTP metadata or not
3. `json-style`: `pretty` or `minified` (applies only when `format=json`)

### Proposed CLI UX

- `--json` (explicit `format=json`)
- `--plain` (explicit `format=plain`)
- `--pretty` (explicit JSON pretty style)
- `--minify` (explicit JSON minified style)
- `--include-http-metadata` (status line + optional headers metadata)
- `--include-headers` (extends metadata details; implies `--include-http-metadata`)

Backward compatibility:

- Keep `--raw` for one transition cycle as alias for `--json --minify`.
- Keep old ADF-specific flags as aliases during migration (details below).

### Default Policy

Default output policy for API commands:

- `format=json`
- `json-style=pretty`
- `include-http-metadata=false`

This makes stdout predictable JSON by default for automation and agents.

### Precedence / Conflict Rules

- `--plain` and `--json` together: validation error.
- `--pretty` and `--minify` together: validation error.
- `--plain` with `--pretty` or `--minify`: validation error.
- `--include-headers` implies `--include-http-metadata`.
- `--raw` with explicit `--pretty`: validation error (during transition).

### Stream separation contract

- For `format=json`:
  - stdout: JSON body only (pretty/minified per style).
  - stderr: optional HTTP metadata when enabled.
- For `format=plain`:
  - stdout: human-readable body; optional metadata prepended when enabled.

This removes JSON+metadata mixing while still allowing metadata visibility.

## Issue-by-Issue Design Proposal

## 1) Metadata mixed with JSON payload

Root cause hypothesis:

- `ResponseFormatter.Format` always prepends status line.
- Pagination path prints status line directly.
- `--raw` does not mean “JSON-only”; it means “no pretty print”.

Proposed behavior:

- JSON mode outputs body-only JSON to stdout.
- HTTP metadata inclusion is optional and separate from body stream.

Implementation approach:

- Introduce `OutputPreferences` (new model object) replacing raw boolean coupling.
- Refactor `ResponseFormatter` to return structured output parts:
  - metadata text
  - formatted body text
- Update `RequestExecutor.HandleResponseAsync` and pagination path to use structured output and stream separation.

Edge cases:

- `204 No Content`: JSON mode should emit empty output (or `{}` only if explicitly defined; choose one and document).
- `--out`: keep body in file only; metadata and saved-file message remain console-visible.

Testing:

- Unit tests for formatter with JSON/non-JSON/empty payload.
- Integration tests asserting exact stdout/stderr contracts in JSON mode.

Docs:

- Update `docs/behavior.md`, `docs/commands/request.md`, and command help text.

## 2) `--xxx-file` vs `--xxx-adf-file` confusion

Root cause hypothesis:

- Similar semantics split across many near-duplicate flags.
- Input format is encoded in option name instead of explicit format metadata.

Proposed behavior:

- Use consistent pattern: `<target>-file` + `<target>-format`.
- Example mappings:
  - `--description-file <path> --description-format adf|text`
  - `--field <name> --field-file <path> --field-format adf|json`
  - `--body-file <path> --body-format json|adf` (for comment flows)

Backward compatibility strategy:

- Keep legacy flags as aliases:
  - `--description-adf-file` -> `--description-file ... --description-format adf`
  - `--field-adf-file` -> `--field-file ... --field-format adf`
  - `--body-adf-file` -> `--body-file ... --body-format adf`
- Mark legacy flags as deprecated in help/docs and mention sunset milestone.

Implementation approach:

- Add shared parser helpers in `IssueCommands` for file+format options.
- Keep existing payload construction logic but route through normalized format-specific helpers.

Testing:

- Command parse tests for new flags.
- Compatibility tests for old aliases.
- Negative tests for invalid format combinations.

Docs:

- Update `docs/commands/jira-shortcuts.md` and use-case playbooks to show new canonical flags first.

## 3) Missing `--plain` mode

Root cause hypothesis:

- Current output control is JSON-centric via `--raw`.
- No explicit format selection.

Proposed behavior:

- Add explicit `--plain` (or `--format plain`) for human-readable output.
- JSON remains default for machine compatibility.

Implementation approach:

- Extend command option set to include `format`.
- Ensure all Jira shortcuts and `request` command use shared output option binding.

Edge cases:

- `--plain` with pagination should produce plain text output without forced JSON pretty logic.
- Plain output for non-JSON payloads should preserve original body text.

Testing:

- Golden output tests for plain mode on JSON and text responses.

Docs:

- Add dedicated plain-mode examples in `docs/commands/request.md`.

## 4) Missing minified JSON option

Root cause hypothesis:

- `--raw` indirectly provides compact behavior but is semantically overloaded.
- No explicit minify intent or conflict rules.

Proposed behavior:

- Add explicit `--minify`.
- Add explicit `--pretty`.
- Keep `--raw` as temporary alias to `--minify`.

Precedence/conflict:

- `--pretty` + `--minify`: validation error.
- If neither provided, default to pretty.

Testing:

- Formatter unit tests for pretty/minified exact output.
- Integration tests for option conflicts.

Docs:

- Replace “raw” framing with “json style” model; keep alias note during transition.

## 5) BOM causing INVALID_INPUT

Root cause hypothesis:

- Payload file reads are scattered and inconsistent.
- No explicit normalization step for leading BOM codepoints across all payload paths.

Proposed behavior:

- Introduce a shared text-file reader that:
  - detects UTF-8/UTF-16 BOM
  - decodes safely
  - strips leading BOM codepoint (`\uFEFF`) when present
- Use it for all request payload text reads (`--body-file`, ADF/json file inputs, JQL file).

Implementation approach:

- New utility in `src/acjr3/Common/` (for example `TextFileInput.cs`).
- Replace direct `File.ReadAllText*` call sites in:
  - `Program.cs`
  - `IssueCommands.cs`
  - `IssueLinkCommands.cs`
  - `SearchCommands.cs`

Binary/attachment safety:

- Scope normalization utility to text-input flags only.
- Do not alter `--out` response streaming path.
- Document that file-body inputs are treated as text payloads.

Testing:

- Unit tests with UTF-8 BOM and UTF-16 BOM files.
- Integration tests confirming sent request body begins with `{` (no BOM prefix) and Jira-like parser accepts payload.

## Proposed Refactors (Files/Modules)

- `src/acjr3/Http/RequestExecution.cs`
  - Replace `Raw`/`IncludeHeaders` usage with output model object.
  - Remove hardcoded pagination status-line print.
- `src/acjr3/Output/ResponseFormatter.cs`
  - Split metadata/body formatting paths.
  - Support JSON style options explicitly.
- `src/acjr3/App/Program.cs`
  - Request command option redesign (`--json`, `--plain`, `--pretty`, `--minify`, metadata options).
  - Migrate file-body read to shared BOM-safe utility.
- `src/acjr3/Commands/Jira/*.cs`
  - Introduce shared output-option helper and apply consistently.
  - Update issue/comment/update option parsing for file+format model.
- `src/acjr3/Common/` (new)
  - Add text input reader with BOM normalization.
- `tests/acjr3.Tests/`
  - Extend `Http/RequestExecutorTests.cs`
  - Extend `Integration/ProgramE2eTests.cs`
  - Add focused tests for BOM utility and flag alias behavior.

## Implementation Phases (PR-Sized)

1. Output model foundation
- Add output preference types and formatter refactor.
- Keep legacy behavior toggled via compatibility alias (`--raw`).

2. Request command UX migration
- Add new output flags to `request`.
- Implement conflict validation + metadata/body stream separation.

3. Jira shortcut output alignment
- Apply shared output options to all shortcut commands.
- Remove per-command drift in output semantics.

4. File input normalization
- Introduce BOM-safe reader and migrate all text file inputs.
- Add regression tests for BOM scenarios.

5. File-flag redesign for issue/comment flows
- Add `<target>-file + <target>-format`.
- Keep legacy `*-adf-file` aliases with deprecation messaging.

6. Documentation + help finalization
- Update docs/help/examples.
- Add migration note and deprecation timeline.

## Acceptance Criteria

1. Raw JSON mode exists and emits only JSON body to stdout.
2. HTTP metadata inclusion is optional and separated from body output.
3. Minified JSON mode exists and works with JSON output mode.
4. `--plain` mode exists for human-readable output.
5. BOM is stripped safely for text payload files, preventing INVALID_INPUT from BOM-prefixed content.
6. Help/docs clearly reduce confusion around file vs ADF-file usage via explicit format model.

## Test Checklist

- Unit:
  - formatter: json pretty/minify/plain + metadata on/off
  - formatter: empty payload and non-JSON payload
  - BOM reader: UTF-8 BOM, UTF-16 LE/BE BOM, no BOM
  - option validation: conflict combinations and alias behavior
- Integration:
  - `request` JSON default outputs body-only JSON on stdout
  - `request --include-http-metadata` writes metadata without corrupting JSON stdout
  - `--plain` output snapshot/golden tests
  - issue/comment/create/update file-format flows using new canonical flags
  - legacy `*-adf-file` flags still function (compatibility tests)
- Regression:
  - existing retry/pagination/fail-on-non-success tests remain green
  - `--out` binary streaming behavior unchanged

## Documentation Plan

Update at least:

- `README.md`
- `docs/behavior.md`
- `docs/commands/request.md`
- `docs/commands/jira-shortcuts.md`
- `docs/commands/README.md`
- `docs/use-cases/*.md` examples that currently use `--raw` or `*-adf-file` as canonical

Doc updates will include:

- New output model and defaults
- Clear precedence/conflict rules
- Canonical input file + input format examples
- Legacy alias deprecation notes

## Migration And Backward Compatibility Notes

- `--raw` remains available as alias to `--minify` during transition.
- Legacy ADF-specific flags remain supported with deprecation messaging.
- Publish migration examples mapping old->new invocations.
- Plan a future release milestone for removing deprecated aliases after docs and tests are stable.

