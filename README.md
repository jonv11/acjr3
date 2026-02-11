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

## Packaging

Local single-file self-contained publish:

```bash
dotnet publish src/acjr3/acjr3.csproj -c Release -p:PublishProfile=SingleFileSelfContained -r linux-x64 --self-contained true
```

GitHub Actions:
- `CI` workflow: build + test + coverage artifact
- `Release` workflow: cross-platform packaged artifacts on `v*` tags (and optional release publishing)

## Documentation

- Start here: `docs/README.md`
- Contributor guide: `CONTRIBUTING.md`
- Setup: `docs/getting-started.md`
- Environment and auth: `docs/configuration.md`
- Command map: `docs/commands/README.md`
- Request command: `docs/commands/request.md`
- Jira shortcut commands: `docs/commands/jira-shortcuts.md`
- OpenAPI helpers: `docs/commands/openapi.md`
- Shell completion: `docs/commands/completion.md`
- Use-case playbooks: `docs/use-cases/README.md`
- Codebase structure: `docs/codebase-structure.md`
- Runtime behavior and known limits: `docs/behavior.md`
- Doc writing conventions: `docs/doc-conventions.md`

## External reference

- Jira Cloud REST API v3: https://developer.atlassian.com/cloud/jira/platform/rest/v3
