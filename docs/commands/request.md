# Request Command

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Jira Shortcuts](jira-shortcuts.md)

`request` is the main low-level command. It can call any Jira REST path.

## Syntax

```bash
acjr3 request <METHOD> <PATH> [options]
```

`METHOD` must be one of: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`.

`PATH` can be with or without leading slash, for example:
- `/rest/api/3/myself`
- `rest/api/3/myself`

## Options

- `--query key=value` (repeatable)
- `--header key=value` (repeatable)
- `--accept <mime>` (default `application/json`)
- `--content-type <mime>`
- `--body <string>`
- `--body-file <path>` (mutually exclusive with `--body`)
- `--out <path>` (streams raw response bytes to file)
- `--raw` (skip JSON pretty print)
- `--include-headers`
- `--fail-on-non-success <true|false>` (default `true`; set `false` to allow `4xx/5xx` with exit code `0`)
- `--dry-run` (print request details, do not send)
- `--verbose` (diagnostics to stderr)
- `--retry-non-idempotent` (enables retries for `POST` and `PATCH`)
- `--paginate` (`GET` only, best effort for `values`-based response pages)

## Examples

```bash
acjr3 request GET /rest/api/3/myself
acjr3 request GET /rest/api/3/search --query "jql=project = TEST" --query "maxResults=10"
acjr3 request POST /rest/api/3/issue --body-file create-issue.json
acjr3 request GET /rest/api/3/project --include-headers --raw
acjr3 request GET /rest/api/3/project --paginate --raw
acjr3 request GET /rest/api/3/attachment/content/10000 --out attachment.bin --include-headers
```

## Common workflows

- Search and keep output compact:
  - `acjr3 request GET /rest/api/3/search --query "jql=project = ACJR ORDER BY updated DESC" --query "maxResults=25" --raw`
- Paginate list endpoints:
  - `acjr3 request GET /rest/api/3/project/search --paginate --raw`
- Export response data to disk safely:
  - `acjr3 request GET /rest/api/3/project --out projects.json`
