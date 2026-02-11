# 10. Bulk Maintenance And Hygiene

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](09-knowledge-extraction-decisions.md)

## Goal

Apply deterministic consistency fixes at scale while preserving auditability and minimizing risk.

## Commands used

- `acjr3 search list`
- `acjr3 issue view`
- `acjr3 issue update`
- `acjr3 issue transition`
- `acjr3 issue comment`

## Step-by-step

1. Run audit-mode searches first.

```bash
acjr3 search list \
  --jql "project = ACJ AND updated <= -30d AND status not in (Done, Closed)" \
  --fields "key,summary,status,labels,updated,assignee" \
  --max-results 200 --raw
```

```bash
acjr3 search list \
  --jql "project = ACJ AND labels is EMPTY AND statusCategory != Done" \
  --fields "key,summary,status,labels" \
  --max-results 200 --raw
```

2. Choose deterministic rules and scope.
- allowlist target projects and issue types
- denylist protected labels or statuses
- define max items per run (for example `<= 50`)

3. Apply small batch updates from JSON payload files.

```bash
acjr3 issue update ACJ-710 --body-file hygiene-update.json --fail-on-non-success
```

4. Transition stale items only when policy allows.

```bash
acjr3 issue transition ACJ-710 --to "Needs Triage" --fail-on-non-success
```

5. Add audit comments for every changed ticket (ADF-first).

```bash
acjr3 issue comment ACJ-710 --body-adf-file hygiene-audit-comment.adf.json
```

6. Verify a post-change sample.

```bash
acjr3 issue view ACJ-710 --fields "status,labels,updated" --raw
```

## Safety checks

- Never bulk-edit without an audit-mode report.
- Start with a small sample size before full run.
- Keep change payloads in versioned files for traceability.
