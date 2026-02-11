# Configuration

Navigation: [Docs Home](README.md) | [Getting Started](getting-started.md) | [Runtime Behavior](behavior.md)

All configuration is read from environment variables.

## Required

- `ACJR3_SITE_URL`: absolute `http` or `https` URL

Auth requirements depend on `ACJR3_AUTH_MODE`:
- `basic` (default): requires `ACJR3_EMAIL` and `ACJR3_API_TOKEN`
- `bearer`: requires `ACJR3_BEARER_TOKEN`

## Optional

- `ACJR3_AUTH_MODE`: `basic` or `bearer` (default `basic`)
- `ACJR3_TIMEOUT_SECONDS`: request timeout in seconds (default `100`, must be > 0)
- `ACJR3_MAX_RETRIES`: retry count (default `5`, must be >= 0)
- `ACJR3_RETRY_BASE_DELAY_MS`: base delay for backoff in ms (default `500`, must be > 0)
- `ACJR3_OPENAPI_CACHE_PATH`: optional full path override for OpenAPI cache file

## Validation

```bash
acjr3 config check
```

`config check` validates auth and prints masked values.

## Config helper commands

```bash
acjr3 config show
acjr3 config set <KEY> <VALUE> [--target process|user]
acjr3 config init [options] [--target process|user]
```

Notes:
- `config set` supports known `ACJR3_*` keys only.
- `config init` applies only provided values and leaves others unchanged.
- Use `--target process` for non-persistent shell/session testing.
- `--target user` persistence behavior is OS/runtime dependent; use `--target process` for portable scripts/CI.

## Security guidance

- Prefer `--target process` for secrets in shared/dev machines and CI.
- Avoid committing token values in shell profiles, scripts, or repository files.
- Rotate Jira API tokens periodically and after any suspected exposure.
- Use least-privilege accounts/tokens for automation where possible.

## Per-invocation overrides

All commands accept runtime overrides that apply only to the current invocation:
- `--site-url`
- `--auth-mode`
- `--email`
- `--api-token`
- `--bearer-token`
- `--timeout-seconds`
- `--max-retries`
- `--retry-base-delay-ms`
- `--openapi-cache-path`

Precedence:
- Command flag override (current invocation only)
- Environment variable value
- Built-in default (for optional timeout/retry values)
