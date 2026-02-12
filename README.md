# acjr3

`acjr3` is a .NET 8 CLI proxy for Jira Cloud REST API v3.

It is built for:
- direct REST access via `acjr3 request ...`
- lightweight shortcut commands for common Jira operations
- agent-safe behavior (structured output, deterministic exit codes, strict validation)

## Quick start

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
dotnet run --project src/acjr3 -- --help
```

## CLI Output Contract

- Default output is a JSON envelope on `stdout`: `success`, `data`, `error`, `meta`.
- Use `--format json|jsonl|text` to switch output format.
- Use `--pretty` or `--compact` for JSON style.
- Use `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, and `--plain` for output shaping.
- Exit codes are deterministic:
  - `0` success
  - `1` validation/bad arguments
  - `2` authentication/authorization
  - `3` not found
  - `4` conflict/business rule
  - `5` network/timeout
  - `10+` internal/tool-specific

## Preferred Input Style

For `request`, use canonical input flags:

- `--in <file|->`
- `--input-format json|adf|md|text`
- Optional JSON base shortcuts: `--body '<json-object>'`, `--body-file <path>`
- `--in`, `--body`, and `--body-file` are mutually exclusive
- For `POST|PUT|PATCH`, if no explicit payload source is provided, `request` sends `{}`.

For Jira issue description/comment field payloads:

- `acjr3 issue create <PROJECT> --summary "..." --description-file <description.adf.json> --description-format adf --yes`
- `acjr3 issue update <KEY> --field description --field-file <description.adf.json> --field-format adf --yes`
- `acjr3 issue comment add <KEY> --in <comment.adf.json> --input-format adf --yes`

## Navigation

- Project docs hub: [docs/README.md](docs/README.md)
- Contributor workflow: [CONTRIBUTING.md](CONTRIBUTING.md)
- Command reference index: [docs/commands/README.md](docs/commands/README.md)
- Use-case playbooks index: [docs/use-cases/README.md](docs/use-cases/README.md)

## Packaging

Local single-file self-contained publish:

```bash
dotnet publish src/acjr3/acjr3.csproj -c Release -p:PublishProfile=SingleFileSelfContained -r linux-x64 --self-contained true
```

GitHub Actions:
- `CI` workflow: build + test + coverage artifact
- `Release` workflow: cross-platform packaged artifacts on `v*` tags (and optional release publishing)

## Documentation

### Start Here

- Documentation hub: [docs/README.md](docs/README.md)
- Getting started: [docs/getting-started.md](docs/getting-started.md)
- Configuration and auth: [docs/configuration.md](docs/configuration.md)

### Commands

- Command index: [docs/commands/README.md](docs/commands/README.md)
- Universal request command: [docs/commands/request.md](docs/commands/request.md)
- Jira shortcuts: [docs/commands/jira-shortcuts.md](docs/commands/jira-shortcuts.md)
- OpenAPI helpers: [docs/commands/openapi.md](docs/commands/openapi.md)
- Shell completion: [docs/commands/completion.md](docs/commands/completion.md)

### Workflows And Internals

- Use-case playbooks: [docs/use-cases/README.md](docs/use-cases/README.md)
- Runtime behavior and limits: [docs/behavior.md](docs/behavior.md)
- Codebase structure: [docs/codebase-structure.md](docs/codebase-structure.md)
- Documentation conventions: [docs/doc-conventions.md](docs/doc-conventions.md)

## External reference

- Jira Cloud REST API v3: https://developer.atlassian.com/cloud/jira/platform/rest/v3
