# Runtime Behavior

Navigation: [Docs Home](README.md) | [Configuration](configuration.md) | [Commands](commands/README.md)

## Output Model

- Default output is a JSON envelope:
  - `success`
  - `data`
  - `error { code, message, details, hint }`
  - `meta { version, requestId, durationMs, statusCode, method, path }`
- Output format is controlled with `--format json|jsonl|text` (default `json`).
- JSON style is controlled with `--pretty` or `--compact`.
- Output shaping flags:
  - `--select`
  - `--filter`
  - `--sort`
  - `--limit`
  - `--cursor`
  - `--page`
  - `--all`
  - `--plain`
- Output option constraints:
  - `--plain` is only valid with text output when `--format` is explicitly set (for example, `--format text --plain`).
  - `--pretty` and `--compact` are mutually exclusive.
  - `--format text` cannot be combined with `--pretty` or `--compact`.
- Payload is emitted on `stdout`. Diagnostics remain on `stderr`.

## Exit Codes

- `0`: success
- `1`: validation / bad arguments
- `2`: authentication / authorization
- `3`: not found
- `4`: conflict / business rule failure
- `5`: network / timeout
- `10+`: internal / tool-specific

## Request Input

- Canonical payload input is `--in <file|->`.
- Canonical payload format is `--input-format json|adf|md|text`.
- Optional JSON base payload inputs are:
  - `--body '<json-object>'`
  - `--body-file <path>`
- `--in`, `--body`, and `--body-file` are mutually exclusive.
- Text payload reads are BOM-normalized.

## JSON Payload Pipeline (Write Commands)

For JSON write commands (`request` mutating methods, `issue create/update/transition`, `issue comment add/update`, `issuelink`), payload processing is deterministic:

1. Initialize an in-memory default payload object.
2. If one explicit base source is provided (`--body`, `--body-file`, or `--in`), replace the default base.
3. Apply command-specific sugar flags (for example `--summary`, `--id`, `--text`) as patches on top of the base.
4. Validate final required fields for the command.
5. Serialize and send the final JSON payload.

When no explicit base is provided:
- `request` with `POST|PUT|PATCH` defaults to `{}`.
- Shortcut commands use endpoint-specific default payload shapes.

## Safety

- Mutating operations require `--yes` or `--force` (for example `request`, `issue create/update/delete`, `issue comment add/update/delete`, `issue transition`, and `issuelink`).
- `--explain` prints endpoint/payload without executing.
- `--request-file` writes a replayable request artifact.
- `--replay <file>` executes a saved request artifact.

## Retries

- Retries on HTTP `429`.
- Retries on HTTP `5xx`.
- Retries on `HttpRequestException` and `TaskCanceledException`.
- Idempotent methods are retried by default: `GET`, `PUT`, `DELETE`.
- `POST` and `PATCH` retries require `--retry-non-idempotent`.
