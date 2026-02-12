# Agent Capabilities Commands

Navigation: [Docs Home](../README.md) | [Commands Index](README.md)

These commands expose machine-readable CLI metadata and diagnostics.

## Syntax

```bash
acjr3 capabilities
acjr3 schema [<command>]
acjr3 doctor [--check-network]
acjr3 auth status
```

## Behavior

- `capabilities` returns supported formats, styles, input formats, and exit-code taxonomy.
- `schema <command>` returns a schema summary for the requested command path.
- `doctor` runs local environment/auth checks and optional network validation.
- `auth status` reports auth mode/configuration status.

All commands support shared output options:
- `--format json|jsonl|text`
- `--pretty` or `--compact`
- `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, `--plain`

## Examples

```bash
acjr3 capabilities --compact
acjr3 schema "issue create"
acjr3 doctor --check-network --compact
acjr3 auth status
```
