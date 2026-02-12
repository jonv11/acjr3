# Request Command

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Jira Shortcuts](jira-shortcuts.md)

`request` is the low-level universal Jira REST command.

## Syntax

```bash
acjr3 request <METHOD> <PATH> [options]
acjr3 request --replay <REQUEST_FILE> [options]
```

`METHOD`: `GET|POST|PUT|DELETE|PATCH`

## Core Input Options

- `--in <PATH|->`: request payload source (`-` means stdin)
- `--input-format json|adf|md|text`: payload format (default `json`)
- `--body '<json-object>'`: inline JSON base payload
- `--body-file <PATH>`: JSON base payload file
- `--in`, `--body`, and `--body-file` are mutually exclusive
- `--query key=value` (repeatable)
- `--header key=value` (repeatable)
- `--accept <mime>` (default `application/json`)
- `--content-type <mime>`
- `--out <PATH>` (write response body to file)

For mutating methods (`POST`, `PUT`, `PATCH`), if no explicit payload source is provided, `request` sends `{}` as the default JSON payload.

## Output Options

- `--format json|jsonl|text` (default `json`)
- `--pretty` or `--compact`
- `--select`, `--filter`, `--sort`, `--limit`, `--cursor`, `--page`, `--all`, `--plain`

Constraints:
- `--plain` is only valid with text output when `--format` is explicitly set (for example, `--format text --plain`).
- `--pretty` and `--compact` are mutually exclusive.
- `--format text` cannot be combined with `--pretty` or `--compact`.

## Execution / Safety Options

- `--fail-on-non-success` (default `true`)
- `--retry-non-idempotent`
- `--paginate`
- `--yes` or `--force` (required for mutating operations)
- `--verbose`, `--debug`, `--trace`
- `--explain` (show endpoint + payload without sending)
- `--request-file <PATH>` (save replayable request artifact)
- `--replay <PATH>` (execute saved request artifact)

## Examples

```bash
acjr3 request GET /rest/api/3/myself
acjr3 request POST /rest/api/3/issue --in create-issue.json --input-format json --yes
acjr3 request POST /rest/api/3/issue --body '{"fields":{"project":{"key":"ACJ"},"summary":"Hello","issuetype":{"name":"Task"}}}' --yes
acjr3 request GET /rest/api/3/search --query "jql=project = ACJ" --format jsonl --compact
acjr3 request GET /rest/api/3/project --explain
acjr3 request --replay .acjr3/request.json
```
