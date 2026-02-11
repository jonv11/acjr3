# Codebase Structure

This layout is designed for growth with clear boundaries.

## Source (`src/acjr3`)

- `App/`: application entrypoint and command wiring (`Program.cs`)
- `Commands/`: CLI command groups
- `Commands/Jira/`: Jira-focused command wrappers
- `Configuration/`: environment config loading and validation
- `Http/`: request execution, retry logic, pagination behavior
- `OpenApi/`: OpenAPI fetch/list/show helpers
- `Output/`: response formatting and output shaping
- `Common/`: shared utilities (logging, auth headers, URL helpers, parsing, redaction)

## Tests (`tests/acjr3.Tests`)

Tests mirror production domains:

- `Configuration/`: config parsing and validation tests
- `Http/`: retry policy and request behavior tests
- `Common/`: helper and utility tests

## Expansion conventions

- Add new code in the closest domain folder, not in root.
- Keep command classes under `Commands/...`.
- Keep shared cross-domain helpers under `Common/`.
- Mirror new source domains in tests.
- Prefer many small files with focused responsibilities.
