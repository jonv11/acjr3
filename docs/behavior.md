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
- `--fail-on-non-success` changes exit code to `1` for `4xx/5xx`.

## Logging

- Runtime logging uses `Microsoft.Extensions.Logging`.
- `--verbose` enables acjr3 debug-level diagnostics (retry attempts, backoff timing, config load diagnostics).
- Non-verbose runs keep logging quiet except explicit command output/errors.

## Exit Codes

- `0`: command completed (including non-success HTTP responses when `--fail-on-non-success` is not set)
- `1`: validation/runtime error, or HTTP `4xx/5xx` when `--fail-on-non-success` is set

## Pagination (`--paginate`)

- Only allowed on `GET`.
- Expects responses containing a top-level `values` array.
- Uses `startAt`, `maxResults`, `isLast`, and `total` when available.
- If structure is not recognized, it falls back to a single request.

## Known implementation limits

- `issue transition` uses transition name, not transition ID lookup.
- `issue create` and `issue comment` send plain strings for description/body fields. Some Jira setups may require ADF objects.
