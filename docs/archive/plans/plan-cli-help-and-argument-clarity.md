# Plan: CLI Help And Argument Clarity Hardening

Status: Validated
Scope: Analysis + implementation plan. No behavior implementation in this plan.

## Goal

Make `acjr3` self-discoverable from CLI help alone, so a user or agent can execute commands correctly without external docs/examples.

## Problem Statement

Current help output is usable but not reliably agent-friendly. Main issues:

1. Option/argument descriptions are inconsistent in style and precision.
2. Some constraints are implicit (only shown as runtime errors, not discoverable in `--help`).
3. Global/local option overlap creates duplicate entries in help output (`config init --help`).
4. Naming is uneven (`e.g.` vs `for example`, `accountId` casing, file option intent wording).
5. Many commands do not expose usage patterns beyond raw option lists.

## Reconnaissance Summary

### Where help text is defined

- Root and shared command wiring:
  - `src/acjr3/App/Program.cs`
- Jira command help text definitions:
  - `src/acjr3/Commands/Jira/IssueCommands.cs`
  - `src/acjr3/Commands/Jira/IssueLinkCommands.cs`
  - `src/acjr3/Commands/Jira/SearchCommands.cs`
  - `src/acjr3/Commands/Jira/ProjectCommands.cs`
  - `src/acjr3/Commands/Jira/UserCommands.cs`
  - `src/acjr3/Commands/Jira/FieldCommands.cs`
  - `src/acjr3/Commands/Jira/GroupCommands.cs`
  - `src/acjr3/Commands/Jira/PriorityCommands.cs`
  - `src/acjr3/Commands/Jira/StatusCommands.cs`
  - `src/acjr3/Commands/Jira/IssueTypeCommands.cs`
  - `src/acjr3/Commands/Jira/RoleCommands.cs`
  - `src/acjr3/Commands/Jira/ResolutionCommands.cs`

### High-impact observed gaps

1. Duplicate option names in help for `config init`:
- Local options (`--site-url`, `--auth-mode`, etc.) plus root global options with same names produce duplicate help rows.

2. Hidden relationship rules:
- Example: `issue create` has mutual exclusion and fallback behavior, but help text does not fully express valid combinations.
- Example: `issue comment` combines legacy and subcommand forms, but help does not clearly prioritize or constrain flows.

3. Inconsistent description language:
- Mixed patterns like `e.g.` and `for example`.
- Mixed input wording (`Read ... from file path`, `Inline JSON payload ...`, `... query parameter ...=true|false`).

4. Option semantics encoded weakly:
- Several boolean query parameters are represented as `Option<string?>`, then validated later.
- Help shows `<value>` but does not consistently reveal exact accepted domain.

5. Sparse intent in descriptions:
- Many options describe syntax but not outcome/side effects.

### Current testing posture

- No dedicated tests enforce help quality or help regressions.
- Existing integration tests focus on runtime behavior, not discoverability or option description quality.

## Design Principles (Agent-First CLI)

1. One command, one clear purpose line.
2. Every argument/option describes:
- what it targets
- accepted shape/domain
- effect on request/output
3. Constraints are discoverable in help (not only runtime errors).
4. Naming follows consistent vocabulary and casing.
5. Help output is deterministic and free from duplicated/conflicting entries.
6. Keep URI-path alignment conventions from repository rules.

## Proposed Help Quality Standard

## 1) Description style guide (enforced convention)

Command description template:
- Verb + Jira resource + scope.
- Example: `List Jira projects (GET /rest/api/3/project/search).`

Argument description template:
- `Resource identifier + format/example`.
- Example: `Issue key in Jira format (for example, ACJ-123).`

Option description template:
- `Action + accepted value shape + effect`.
- Example: `Include only these fields (comma-separated field keys).`

Language standardization:
- Use `for example` (avoid mixed `e.g.` usage).
- Use `account ID` in prose; keep API field name `accountId` only where required.
- Use `path to ... file` consistently for file inputs.

## 2) Explicit constraint surfacing in help

For commands with mutually exclusive or dependent options, include one-line constraint notes directly in option descriptions or command description footer.

Examples:
- `Use either --body/--body-file or --summary + project options.`
- `Use --field together with --field-file when updating one ADF/JSON field payload.`

## 3) Canonical naming normalization

Define canonical option families (reused across commands):

- Output family: `--json`, `--plain`, `--pretty`, `--minify`, metadata flags (from output cleanup plan).
- Input payload family: `--body`, `--body-file`, format companion flags.
- Common execution family: `--fail-on-non-success`, `--verbose`.

Ensure help wording is shared and identical across commands for the same semantic option.

## 4) Remove duplicated help entries

Refactor root/global option injection strategy:

- Stop attaching runtime override globals to all commands by default.
- Apply override options only where they are meaningful and non-conflicting.
- Prevent duplicate option names under `config` subcommands.

## 5) Add minimal embedded examples in CLI

Add short usage examples discoverable from CLI help output:

- Either embedded in command description (multi-line) or via a consistent `Examples:` section strategy.
- Keep 1-3 command examples per complex command (`request`, `issue create`, `issue comment`, `issue update`, `search list`).

## Proposed Refactors (Modules/Files)

- `src/acjr3/App/Program.cs`
  - Introduce explicit helper to register options per command group (instead of blanket root globals).
  - Normalize root command descriptions and help phrasing.
- `src/acjr3/Commands/Jira/*.cs`
  - Apply shared description templates and standardized wording.
  - Make option constraints visible in help text.
- New shared helper(s), likely under `src/acjr3/Common/`:
  - `CliHelpConventions` (description templates/constants)
  - `CliOptionFactory` or `CommonOptionCatalog` (shared option builders with consistent descriptions/help names)

## Option/Argument Naming Cleanup Targets

Priority targets:

1. `IssueCommands`:
- clarify `project` argument vs `--project`
- clarify `--field` + file companion semantics
- unify comment command forms (`issue comment` direct vs subcommands)

2. `request` command:
- clarify method/path semantics and payload mode
- include explicit note about `--query`/`--header` repeatability

3. `config` commands:
- remove duplicate help rows caused by overlapping local/global options
- clarify `--target` behavior and side effects

4. Cross-cutting:
- standardize all `--*-file` descriptions
- standardize bool option descriptions and accepted values

## Validation/Error Message Plan (Help-aligned)

When validation fails, error messages should:

1. reference exact conflicting options
2. include one “valid shape” hint
3. avoid requiring docs lookup

This will align runtime guidance with what help already advertises.

## Testing Plan

## Unit-level

- Add tests for help text generation of selected high-value commands:
  - `acjr3 --help`
  - `acjr3 request --help`
  - `acjr3 issue create --help`
  - `acjr3 issue update --help`
  - `acjr3 issue comment --help`
  - `acjr3 config init --help`
- Assert no duplicated option rows for same option token within a command.

## Integration / snapshot (“golden”) tests

- Add stable help snapshots for the commands above.
- Validate that declared constraints appear in help text.
- Validate that required markers and defaults are visible where expected.

## Regression checks

- Existing runtime tests remain green.
- `dotnet run --project src/acjr3 -- <command> --help` still works for all command groups.

## Documentation Plan

Although goal is CLI-first operation, docs still must match help:

- Update `docs/commands/request.md` and `docs/commands/jira-shortcuts.md` phrasing to mirror CLI text exactly.
- Update `docs/behavior.md` where option semantics are clarified.
- Add short “CLI-first usage” note in `README.md` and `docs/commands/README.md`.

## Implementation Phases (PR-Sized)

1. Help style baseline and shared convention helpers
- Introduce help text standards and shared helper utilities.

2. High-traffic command hardening
- Update help/names/descriptions for `request`, `issue create/update/comment`, `search list`.

3. Global option injection cleanup
- Remove/avoid duplicated options in config help.

4. Full command-family normalization
- Apply standards across remaining Jira commands.

5. Help snapshot test suite + docs sync
- Add regression tests and finalize docs/help parity.

## Acceptance Criteria

1. A user/agent can derive valid command usage for core flows from `--help` only.
2. No command help output contains duplicate option names with conflicting/duplicated descriptions.
3. Key mutual-exclusion/dependency rules are visible in help output.
4. Argument and option descriptions follow one consistent style guide.
5. Core command help coverage is protected by automated tests.
6. Help text and docs are synchronized.

## Backward Compatibility

- Keep existing command/option tokens unless there is a strong ambiguity reason.
- If any rename is introduced, provide alias and deprecation text in help.
- Prefer description and discoverability improvements first, token renames second.

