# OpenAPI Commands

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Request Command](request.md)

OpenAPI commands are optional helpers. They do not affect request execution.

## Syntax

```bash
acjr3 openapi fetch [--out <PATH>] [--spec-url <URL>]
acjr3 openapi paths [--path-filter <TEXT>] [--spec-file <PATH>]
acjr3 openapi show <METHOD> <PATH> [--spec-file <PATH>]
```

## Behavior

- `fetch` tries Jira v3 spec URLs and saves JSON to local cache when `--out` is not provided.
- Default cache file: `<LocalApplicationData>/acjr3/openapi-v3.json` (for example on Windows: `%LOCALAPPDATA%\acjr3\openapi-v3.json`).
- Cache path can be overridden with `ACJR3_OPENAPI_CACHE_PATH` (or `--openapi-cache-path` for one invocation).
- `paths` lists `METHOD PATH (operationId)`.
- `show` prints operation basics: params, request content types, and responses.

## When to use `--spec-file`

Use `--spec-file` when:
- network fetch fails
- you want to inspect a pinned local spec version
