# Commands

Navigation: [Docs Home](../README.md) | [Repository Home](../../README.md) | [Use Cases](../use-cases/README.md)

## Core commands

- `acjr3 request <METHOD> <PATH> [options]`: universal Jira REST proxy
- `acjr3 config ...`: configuration helpers (`check`, `show`, `set`, `init`)
- `acjr3 openapi ...`: fetch and inspect OpenAPI metadata
- `acjr3 capabilities`: machine-readable CLI capability summary
- `acjr3 schema <command>`: schema summary for a command surface
- `acjr3 doctor`: environment/auth/cache diagnostics
- `acjr3 auth status`: auth configuration status

## Jira shortcut commands

- `acjr3 issue ...`
- `acjr3 search ...`
- `acjr3 priority ...`
- `acjr3 status ...`
- `acjr3 project ...`
- `acjr3 issuetype ...`
- `acjr3 issuelink ...`
- `acjr3 user ...`
- `acjr3 field ...`
- `acjr3 group ...`
- `acjr3 role ...`
- `acjr3 resolution ...`

For issue description/comment content, prefer canonical input options:
- `--in <PATH|-> --input-format json|adf|md|text`
- Optional JSON base shortcuts: `--body '<json-object>'` and `--body-file <PATH>`
- `--in`, `--body`, and `--body-file` are mutually exclusive
- `--yes` or `--force` for mutating issue shortcuts
- Additional field helpers:
- `--description-file <PATH> --description-format adf`
- `--field <FIELD_KEY> --field-file <PATH> --field-format adf`

## Detailed pages

- [Request command](request.md)
- [Jira shortcuts](jira-shortcuts.md)
- [OpenAPI helpers](openapi.md)
- [Agent capabilities](agent-capabilities.md)
- [Shell completion](completion.md)
- [Essential coverage priority](essential-coverage-priority.md)
- [Essential coverage testing guide](essential-coverage-testing-guide.md)
