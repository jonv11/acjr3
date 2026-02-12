# Contributing

This project is a Jira Cloud REST API v3 CLI wrapper.  
Contributions must keep behavior predictable, docs accurate, and command structure aligned with REST paths.

## Core Rules

1. Every feature change must include documentation updates.
2. Every feature change must include tests, or a clear reason why tests are not practical.
3. Jira shortcut command/subcommand structure must reflect the REST v3 URI path.
4. Prefer small, focused files and domain-based folder placement.

## Automation Policies

- CI enforces a minimum total line coverage of `60%`.
- Dependabot opens weekly dependency update PRs for NuGet and GitHub Actions.

## Command Structure Rule (URI Mapping)

Map from `/rest/api/3/...` to CLI as directly as possible:

- Static URI segment -> command/subcommand token.
- Path parameter (`{...}`) -> CLI argument.
- Query parameter -> explicit CLI option.
- Request body -> flags for common fields and/or `--body` / `--body-file`.

Examples:

- `GET /rest/api/3/project` -> `acjr3 project list`
- `GET /rest/api/3/project/{projectKey}/components` -> `acjr3 project component list --project <KEY>`
- `PUT /rest/api/3/issue/{issueIdOrKey}` -> `acjr3 issue update <KEY> ...`
- `DELETE /rest/api/3/issue/{issueIdOrKey}` -> `acjr3 issue delete <KEY> ...`

If you intentionally diverge from path shape, document the reason in:
- command help text
- `docs/commands/jira-shortcuts.md`

## Project Layout

- Source: `src/acjr3`
- Tests: `tests/acjr3.Tests`

Use existing domains:
- `App/`, `Commands/`, `Configuration/`, `Http/`, `OpenApi/`, `Output/`, `Common/`

Mirror source domains in tests.

## Required Updates Per Feature

When adding/changing a command:

1. Update implementation under `src/acjr3/Commands/...`.
2. Register command in `src/acjr3/App/RootCommandFactory.cs` (if new root/group command).
3. Update docs:
- `docs/commands/jira-shortcuts.md` for Jira shortcut commands
- `docs/commands/request.md` if universal request behavior changes
- `docs/behavior.md` for retries/output/pagination/runtime changes
- `README.md` or `docs/README.md` when navigation changes
4. Add/update tests under `tests/acjr3.Tests/...`.

## Verification Checklist

Run before opening a PR:

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
dotnet run --project src/acjr3 -- --help
dotnet run --project src/acjr3 -- <command> --help
```

For new endpoint wrappers, also validate against OpenAPI:

```bash
dotnet run --project src/acjr3 -- openapi fetch
dotnet run --project src/acjr3 -- openapi show <METHOD> <PATH>
```

## Definition Of Done

A change is complete only when:

- command shape follows URI mapping rule
- docs reflect current behavior/options/defaults
- tests pass
- help output is correct for changed commands
- no unrelated files are modified

## Pull Requests

Use the PR template at `.github/pull_request_template.md`.  
It mirrors this guide and is required for consistent reviews.

## Copilot

Repository Copilot rules are defined in `.github/copilot-instructions.md`.
