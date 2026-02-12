# acjr3

`acjr3` is a .NET 8 CLI proxy for Jira Cloud REST API v3. It provides a universal `request` command plus Jira shortcut commands with consistent JSON envelopes, deterministic exit codes, and strict validation for mutating operations.

## Quickstart

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
dotnet run --project src/acjr3 -- --help
```

## Most Common Workflows

Run these after setting required environment variables.

1. Validate configuration and auth

```bash
dotnet run --project src/acjr3 -- config check
```

2. Search issues

```bash
dotnet run --project src/acjr3 -- search list --jql "project = ACJ ORDER BY updated DESC" --max-results 20 --compact
```

3. View one issue

```bash
dotnet run --project src/acjr3 -- issue view ACJ-123 --fields "summary,status,assignee,priority" --compact
```

4. Create an issue

```bash
dotnet run --project src/acjr3 -- issue create ACJ --summary "Investigate API timeout" --type Task --yes --fail-on-non-success
```

5. Add a comment

```bash
dotnet run --project src/acjr3 -- issue comment add ACJ-123 --text "Working on this now." --yes --fail-on-non-success
```

## Configuration And Auth

Configuration is environment-variable based, with optional per-invocation runtime overrides on API/runtime commands.

- Canonical reference: [docs/configuration.md](docs/configuration.md)
- Getting started: [docs/getting-started.md](docs/getting-started.md)

## Output And Exit Codes

- Default output is a JSON envelope: `success`, `data`, `error`, `meta`.
- Formats: `--format json|jsonl|text`
- JSON style: `--pretty` or `--compact`
- Output shaping: `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, `--plain`
- Exit codes: `0` success, `1` validation, `2` auth/authz, `3` not found, `4` conflict, `5` network/timeout, `10+` internal/tool-specific

See [docs/behavior.md](docs/behavior.md) for the full runtime contract.

## Docs

- Docs hub: [docs/README.md](docs/README.md)
- Command reference: [docs/commands/README.md](docs/commands/README.md)
- Use-case playbooks: [docs/use-cases/README.md](docs/use-cases/README.md)
- Contributor workflow: [CONTRIBUTING.md](CONTRIBUTING.md)

## External Reference

- Jira Cloud REST API v3: https://developer.atlassian.com/cloud/jira/platform/rest/v3
