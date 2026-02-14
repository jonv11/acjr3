# Getting Started (Users)

Navigation: [Docs Home](README.md) | [Configuration](configuration.md) | [Commands](commands/README.md) | [Developer Setup](developer-setup.md)

This page is for end users running an installed `acjr3` binary.

## Prerequisites

- Jira Cloud site URL (for example `https://your-domain.atlassian.net`)
- Jira credentials for one auth mode (`basic` or `bearer`)
- For `basic`: email + API token
- For `bearer`: bearer token

## Install

Install `acjr3` from this repository's GitHub Releases page and place the binary on your `PATH`.

Release artifacts are produced for:
- `win-x64` (`.zip`)
- `linux-x64` (`.tar.gz`)
- `osx-x64` (`.tar.gz`)

If you prefer to build from source, use [developer-setup.md](developer-setup.md).

## First Command

```bash
acjr3 --help
```

## Minimal Setup Check

After setting required environment variables:

```bash
acjr3 config check
```

## Next Steps

- Configuration reference: [configuration.md](configuration.md)
- Command reference: [commands/README.md](commands/README.md)
- Runtime behavior and exit codes: [behavior.md](behavior.md)
