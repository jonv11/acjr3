# Jira Shortcut Commands

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Request Command](request.md)

Shortcut commands wrap fixed Jira REST API v3 paths.

## Shared Output Options

All Jira shortcut commands support:

- `--format json|jsonl|text`
- `--pretty` or `--compact`
- `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, `--plain`

## Common Runtime Options

- `--fail-on-non-success`
- `--verbose`
- runtime config overrides (`--site-url`, `--auth-mode`, `--email`, `--api-token`, `--bearer-token`, `--timeout-seconds`, `--max-retries`, `--retry-base-delay-ms`, `--openapi-cache-path`)

## Content Input Conventions

- Mutating issue shortcuts use canonical request input:
  - `--in <PATH|->`
  - `--input-format json|adf|md|text`
  - `--yes` or `--force`
- Optional JSON base payload shortcuts:
  - `--body '<json-object>'`
  - `--body-file <PATH>`
  - `--in`, `--body`, and `--body-file` are mutually exclusive
- Field/description helpers remain available:
  - `--description-file <PATH> --description-format text|adf`
  - `--field <FIELD_KEY> --field-file <PATH> --field-format json|adf`

For JSON write shortcuts, payload processing is uniform:
1. Start from a command-specific default payload object.
2. Replace the base with one explicit source (`--body`, `--body-file`, or `--in`) when provided.
3. Apply command-specific sugar flags.
4. Validate required payload fields, then send.

## Comment Commands

Canonical comment command shape:

- `acjr3 issue comment add ...`
- `acjr3 issue comment list ...`
- `acjr3 issue comment get ...`
- `acjr3 issue comment update ...`
- `acjr3 issue comment delete ...`

For comment add/update:
- use `--text "<message>"` for inline text to ADF conversion, or
- use `--in <PATH|-> --input-format adf|json|md|text` for explicit payload input, or
- use `--body` / `--body-file` for explicit JSON base payload input.
