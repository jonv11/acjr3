**AI-Agent Friendly CLI Checklist (for `acjr3`)**

---

## 1) Output & Formatting

* [ ] Default output is **machine-readable JSON**
* [ ] Support `--format json|jsonl|text`
* [ ] **No mixed output** (payload only on stdout)
* [ ] Logs, warnings, progress go to **stderr**
* [ ] Consistent JSON envelope:

  * [ ] `success`
  * [ ] `data`
  * [ ] `error { code, message, details, hint }`
  * [ ] `meta { version, requestId }`
* [ ] Support `--pretty` (human) and `--compact` (minified)
* [ ] Deterministic field ordering (stable output)

---

## 2) Exit Codes (Deterministic)

* [ ] `0` Success
* [ ] `1` Validation / bad arguments
* [ ] `2` Authentication / authorization
* [ ] `3` Not found
* [ ] `4` Conflict / business rule failure
* [ ] `5` Network / timeout
* [ ] `10+` Reserved for tool-specific cases
* [ ] Exit codes documented in `--help`

---

## 3) Command Structure

* [ ] Clear **resource-oriented model** (`acjr3 issue get`, `issue update`, etc.)
* [ ] Consistent verb naming (get, list, create, update, delete)
* [ ] No ambiguous or duplicate commands
* [ ] All commands support `--help`
* [ ] Examples included in help output

---

## 4) Input Handling

* [ ] Single canonical input mechanism:

  * [ ] `--in <file>` or `--in -` (stdin)
* [ ] Explicit format:

  * [ ] `--input-format json|adf|md|text`
* [ ] UTF-8 enforced
* [ ] BOM automatically detected and removed
* [ ] Clear error with file name and position on parse failure

---

## 5) Non-Interactive Behavior

* [ ] No prompts by default
* [ ] Interactive mode only with `--interactive`
* [ ] Destructive operations require:

  * [ ] `--yes` or `--force`
* [ ] All commands safe for unattended execution

---

## 6) Composability (Shell Friendly)

* [ ] Works correctly in pipes
* [ ] Supports stdin (`--in -`)
* [ ] Supports `--out <file>`
* [ ] Supports `--plain` (single value only)
* [ ] No ANSI colors unless `--color`
* [ ] Newline-terminated output

---

## 7) Filtering & Projection

* [ ] Field selection: `--select a,b,c`
* [ ] Nested selection supported (`fields.summary`)
* [ ] Filtering: `--filter <expression>`
* [ ] Sorting: `--sort field[:asc|desc]`
* [ ] Pagination:

  * [ ] `--limit`
  * [ ] `--cursor` or `--page`
  * [ ] `--all`

---

## 8) Determinism & Idempotency

* [ ] List results returned in stable order (or explicit `--sort`)
* [ ] Write operations support `--dry-run`
* [ ] Optional `--diff` before update
* [ ] Safe retry behavior where possible

---

## 9) Error Handling (Agent-Readable)

* [ ] Errors returned as JSON when `--format json`
* [ ] Error codes are stable and documented
* [ ] Include actionable `hint`
* [ ] No stack traces unless `--debug`

---

## 10) Observability & Debugging

* [ ] `--verbose` (human logs to stderr)
* [ ] `--debug` (technical details)
* [ ] `--trace` (structured request/response on stderr)
* [ ] Each operation includes `requestId`
* [ ] Timing info available in debug mode

---

## 11) Capabilities & Self-Description

* [ ] `acjr3 --version` (tool + schema version)
* [ ] `acjr3 capabilities` (formats, features)
* [ ] `acjr3 schema <command>` (input/output schema)
* [ ] `acjr3 doctor` (env/auth/config validation)
* [ ] `acjr3 auth status`

---

## 12) Time & Network Control

* [ ] `--timeout <seconds>`
* [ ] Retry with backoff for transient failures
* [ ] Clear network error classification

---

## 13) Consistency & Naming

* [ ] Flags use kebab-case
* [ ] No synonymous flags (single canonical option)
* [ ] Consistent naming across commands
* [ ] Backward compatibility policy documented

---

## 14) Safety

* [ ] Explicit confirmation for destructive actions
* [ ] No silent data loss
* [ ] Input validation before API calls
* [ ] Clear distinction between client vs server errors

---

## 15) Agent-Optimization Extras (High Value)

* [ ] `--plain` for scalar extraction
* [ ] `--explain` (shows API endpoint + payload without executing)
* [ ] `--request-file` (save generated payload)
* [ ] `--replay <file>` (re-execute saved request)
* [ ] JSONL mode for large lists

---

## 16) Documentation Quality

* [ ] Command examples for common agent workflows
* [ ] Machine-readable examples (copy-paste ready)
* [ ] Help output kept in sync with implementation
* [ ] Exit codes and error schema documented

---

This checklist can be used as:

* a design standard,
* a PR review template,
* or a validation matrix before release.
