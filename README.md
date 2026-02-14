# acjr3

`acjr3` is a .NET 8 CLI proxy for Jira Cloud REST API v3. It provides a universal `request` command plus Jira shortcut commands with consistent JSON envelopes, deterministic exit codes, and strict validation for mutating operations.

## Install (Users)

```bash
# install an artifact from this repository's GitHub Releases page
# then ensure the acjr3 binary is on your PATH
acjr3 --help
```

Release artifacts are published for `win-x64`, `linux-x64`, and `osx-x64`.

## Quickstart (Users)

Run these after setting required environment variables.

```bash
acjr3 config check
```

```bash
acjr3 search list --jql "project = ACJ ORDER BY updated DESC" --max-results 20 --compact
acjr3 issue view ACJ-123 --fields "summary,status,assignee,priority" --compact
acjr3 issue create ACJ --summary "Investigate API timeout" --type Task --yes
acjr3 issue comment add ACJ-123 --text "Working on this now." --yes
```

## Configuration And Auth

Configuration is environment-variable based, with optional per-invocation runtime overrides on API/runtime commands.

- User setup guide: [docs/getting-started.md](docs/getting-started.md)
- Configuration reference: [docs/configuration.md](docs/configuration.md)

## Command Reference

- Commands index: [docs/commands/README.md](docs/commands/README.md)
- Jira shortcut commands: [docs/commands/jira-shortcuts.md](docs/commands/jira-shortcuts.md)
- Universal request command: [docs/commands/request.md](docs/commands/request.md)

## Runtime Contract

Behavior, output envelope, and exit codes: [docs/behavior.md](docs/behavior.md)

## Contributors

- Contributor guide: [CONTRIBUTING.md](CONTRIBUTING.md)
- Source-based setup and local run workflow: [docs/developer-setup.md](docs/developer-setup.md)

## For AI Agents

- Repository agent instructions: [AGENTS.md](AGENTS.md)
- Contributor and workflow policy: [CONTRIBUTING.md](CONTRIBUTING.md)
- Documentation index: [docs/README.md](docs/README.md)

## External Reference

- Jira Cloud REST API v3: https://developer.atlassian.com/cloud/jira/platform/rest/v3
