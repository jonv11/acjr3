# Jira Shortcut Commands

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Request Command](request.md)

Shortcut commands wrap fixed Jira REST API v3 paths.

## Shared Output Options

All Jira shortcut commands support:

- `--format json|jsonl|text`
- `--pretty` or `--compact`
- `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, `--plain`

## Common Runtime Options

- `--allow-non-success`
- `--verbose`
- runtime config overrides (`--site-url`, `--auth-mode`, `--email`, `--api-token`, `--bearer-token`, `--timeout-seconds`, `--max-retries`, `--retry-base-delay-ms`, `--openapi-cache-path`)

## Content Input Conventions

- Mutating issue shortcuts use canonical request input:
  - `--in <PATH|->`
  - `--in` payload must be a JSON object
  - `--yes` or `--force`
- Field/description helpers remain available:
  - `--description-file <PATH> --description-format json|adf`
  - `--field <FIELD_KEY> --field-file <PATH> --field-format json|adf`
  - default format for these file helpers is `adf`

For JSON write shortcuts, payload processing is uniform:
1. Start from a command-specific default payload object.
2. Replace the base with `--in` when provided.
3. Apply command-specific sugar flags.
4. Validate required payload fields, then send.

For `issue create`, project can be provided as positional `<project>` shorthand or `--project <KEY>`.
When both are provided, `--project` takes precedence.

## Comment Commands

Canonical comment command shape:

- `acjr3 issue comment add ...`
- `acjr3 issue comment list ...`
- `acjr3 issue comment get ...`
- `acjr3 issue comment update ...`
- `acjr3 issue comment delete ...`

For comment add/update:
- use `--text "<message>"` for inline text to ADF conversion, or
- use `--text-file <PATH> --text-format json|adf` to patch `body` directly from file content (no inline text wrapping), or
- use `--in <PATH|->` for explicit JSON payload input.
- `--text` and `--text-file` are mutually exclusive.
- `--text-format` requires `--text-file`.
- default `--text-format` is `adf`.

Targeted extract output for read flows:
- `issue view <KEY> --extract <FIELD_NAME>` returns only `fields.<FIELD_NAME>` JSON from the issue payload.
- `issue comment get <KEY> <ID> --extract` returns only `body` JSON from the comment payload.
- Extract mode requires `--format json`.
- Extract mode cannot be combined with `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, or `--plain`.

Comment examples:

```bash
acjr3 issue comment add ACJ-123 --text "Working on this now." --yes
acjr3 issue comment add ACJ-123 --text-file comment-body.adf.json --text-format adf --yes
acjr3 issue comment list ACJ-123 --start-at 0 --max-results 20 --order-by "-created"
acjr3 issue comment get ACJ-123 10001 --expand renderedBody
acjr3 issue comment get ACJ-123 10001 --extract --compact
acjr3 issue comment update ACJ-123 10001 --text "Update from CLI" --notify-users true --yes
acjr3 issue comment delete ACJ-123 10001 --yes
acjr3 issue view ACJ-123 --extract description --compact
```

Boolean-valued query options require `true|false` strings, for example:
- `issue comment update --notify-users true --override-editable-flag false`
- `issue delete --delete-subtasks true`
- `issue transition list --skip-remote-only-condition true --include-unavailable-transitions false --sort-by-ops-bar-and-status true`

## High-Risk Mutating Issue Flows

Issue delete:

```bash
acjr3 issue delete ACJ-123 --delete-subtasks true --yes
```

Issue transition by explicit transition ID:

```bash
acjr3 issue transition ACJ-123 --id 31 --yes
```

Issue transition by transition name (`--to` resolves transition ID first):

```bash
acjr3 issue transition ACJ-123 --to "Done" --yes
```
