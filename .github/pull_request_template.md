## Summary

Describe the change and why it is needed.

## REST Path Mapping

For command/subcommand changes, document the URI mapping:

- REST endpoint(s):
- CLI command(s):
- Mapping rationale:

## Changes

- [ ] Code updated
- [ ] Command help text reviewed
- [ ] Docs updated
- [ ] Tests added or updated

## Documentation Updated

List files changed (for example `docs/commands/jira-shortcuts.md`, `docs/behavior.md`, `README.md`).

## Test Evidence

Paste command output or summary:

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
dotnet run --project src/acjr3 -- --help
```

If command behavior changed, also include:

```bash
dotnet run --project src/acjr3 -- <command> --help
```

If new Jira endpoint wrappers were added, include:

```bash
dotnet run --project src/acjr3 -- openapi show <METHOD> <PATH>
```

## Contributor Checklist

- [ ] Command/subcommand structure reflects REST v3 URI path
- [ ] Path params are CLI args
- [ ] Query params are explicit CLI options
- [ ] Request body handling documented (`--body` / `--body-file` and/or structured flags)
- [ ] Docs reflect current behavior/options/defaults
- [ ] No unrelated files modified
