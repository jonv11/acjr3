# Runtime Behavior

Navigation: [Docs Home](README.md) | [Configuration](configuration.md) | [Commands](commands/README.md)

## Retries

- Retries on HTTP `429`.
- Retries on HTTP `5xx`.
- Retries on `HttpRequestException`.
- Retries on `TaskCanceledException`.
- Idempotent methods are retried by default: `GET`, `PUT`, `DELETE`.
- `POST` and `PATCH` retries require `--retry-non-idempotent`.
- Retry delay uses `Retry-After` on `429` when available.
- Otherwise it uses exponential backoff with jitter (30s cap).

## Output

- Normal output always starts with `HTTP <code> <reason>`.
- JSON is pretty-printed unless `--raw` is set.
- `--include-headers` prints response headers.
- `--out` streams response body bytes to file (no full in-memory body buffering).
- When `--out` is set, console output is status/header-focused plus a saved-file message.
- Non-success HTTP responses (`4xx/5xx`) return exit code `1` by default.
- `--fail-on-non-success false` allows non-success responses to return exit code `0`.

## Logging

- Runtime logging uses `Microsoft.Extensions.Logging`.
- `--verbose` enables acjr3 debug-level diagnostics (retry attempts, backoff timing, config load diagnostics).
- Non-verbose runs keep logging quiet except explicit command output/errors.

## Exit Codes

- `0`: command completed (including non-success HTTP responses only when `--fail-on-non-success false` is set)
- `1`: validation/runtime error, or HTTP `4xx/5xx` by default

## Pagination (`--paginate`)

- Only allowed on `GET`.
- Expects responses containing a top-level `values` array.
- Uses `startAt`, `maxResults`, `isLast`, and `total` when available.
- If structure is not recognized, it falls back to a single request.

## Known implementation limits

- `issue transition` uses transition name, not transition ID lookup.
- `issue create`/`issue update` default to plain string description values unless you use `--description-adf-file` or `--field ... --field-adf-file`.
- `issue comment` default text flow builds ADF automatically; `--body-adf-file` wraps raw ADF JSON under `body`.
