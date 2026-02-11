# GitHub Copilot Instructions

These instructions apply to all Copilot-assisted changes in this repository.

## Primary Rules

1. Always follow repository guidelines and conventions first.
2. Keep command/subcommand design aligned with Jira REST v3 URI paths.
3. Use only official Atlassian Jira Cloud REST API v3 documentation as source of truth.
4. Do not introduce commands for undocumented endpoints.
5. Do not use deprecated endpoints.
6. Avoid experimental endpoints unless explicitly requested and approved.
7. Any feature change must update docs and tests (or explain why tests are not practical).

## Project References (Read First)

- Contributor workflow: `CONTRIBUTING.md`
- Documentation index: `docs/README.md`
- Command references: `docs/commands/README.md`
- Jira shortcut command rules: `docs/commands/jira-shortcuts.md`
- Runtime behavior notes: `docs/behavior.md`
- Codebase structure: `docs/codebase-structure.md`
- Documentation conventions: `docs/doc-conventions.md`

## Jira API Source Of Truth

Use only:

- Jira Cloud REST API v3: https://developer.atlassian.com/cloud/jira/platform/rest/v3

When implementing a new command:

1. Validate endpoint exists in official v3 docs.
2. Prefer non-deprecated, stable endpoints.
3. Map endpoint shape to CLI:
- static path segments -> command/subcommand tokens
- path params -> CLI arguments
- query params -> explicit CLI options
- request body -> structured flags and/or `--body` / `--body-file`
4. If there is ambiguity, verify with local OpenAPI helpers:
- `acjr3 openapi fetch`
- `acjr3 openapi show <METHOD> <PATH>`

## Command Design Conventions

- Keep command names predictable and path-oriented.
- Keep option names explicit and readable.
- Preserve backward compatibility when possible.
- Reuse existing execution patterns (`RequestExecutor`, config validation, output conventions).

## Quality Gates Before Completing Work

- Build succeeds: `dotnet build acjr3.sln`
- Tests pass: `dotnet test acjr3.sln`
- Help output reflects changes:
- `dotnet run --project src/acjr3 -- --help`
- `dotnet run --project src/acjr3 -- <command> --help`
- Docs updated to match actual behavior/options/defaults.

## Safety And Accuracy

- Do not invent Jira behavior not present in official docs.
- Do not silently change auth, retry, pagination, or output semantics without documentation updates.
- Do not introduce unrelated changes.
