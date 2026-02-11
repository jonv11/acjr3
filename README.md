# acjr3

`acjr3` is a .NET 8 CLI proxy for Jira Cloud REST API v3.

It is built for:
- direct REST access via `acjr3 request ...`
- lightweight shortcut commands for common Jira operations
- predictable behavior (config validation, retries, clean output)

## Quick start

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
dotnet run --project src/acjr3 -- --help
```

## Preferred Jira payload style

For Jira issue descriptions and comments, prefer ADF-file options over raw JSON payload wrappers:

- `acjr3 issue create <PROJECT> --summary "..." --description-adf-file <description.adf.json>`
- `acjr3 issue update <KEY> --field description --field-adf-file <description.adf.json>`
- `acjr3 issue comment add <KEY> --body-adf-file <comment.adf.json>`

Use `--body` or `--body-file` when you need advanced/multi-field payloads that go beyond one description/comment content object.

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
