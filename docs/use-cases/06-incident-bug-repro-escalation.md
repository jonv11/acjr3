# 6. Incident Or Bug Reproduction And Escalation

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](05-daily-execution-loop.md) | [Next](07-dependency-blocker-management.md)

## Goal

Capture reproducible evidence quickly, update severity/impact consistently, and escalate when needed.

## Commands used

- `acjr3 issue view`
- `acjr3 issue update`
- `acjr3 issue comment`
- `acjr3 issuelink`
- `acjr3 issue transition`

## Step-by-step

1. Pull active bug queue.

```bash
acjr3 search list \
  --jql "project = ACJ AND issuetype = Bug AND status not in (Done, Closed) ORDER BY priority DESC, updated DESC" \
  --fields "key,summary,priority,status,labels" \
  --max-results 50 --raw
```

2. Inspect existing bug details.

```bash
acjr3 issue view ACJ-333 \
  --fields "summary,description,priority,labels,environment,fixVersions,issuelinks" \
  --raw
```

3. Apply bug template update (`Steps / Expected / Actual / Env / Regression`) via ADF description.

```bash
acjr3 issue update ACJ-333 --field description --field-adf-file bug-evidence-description.adf.json --fail-on-non-success
```

4. Request missing artifacts and confirm repro (ADF-first).

```bash
acjr3 issue comment ACJ-333 --body-adf-file repro-request-comment.adf.json
```

5. Add duplicate or causal link.

```bash
acjr3 issuelink --type "Duplicate" --inward "ACJ-333" --outward "ACJ-289" --fail-on-non-success
```

6. Escalate workflow state.

```bash
acjr3 issue transition ACJ-333 --to "Investigating" --fail-on-non-success
```

7. Verify severity and status.

```bash
acjr3 issue view ACJ-333 --fields "status,priority,labels,environment,issuelinks" --raw
```

## Accuracy notes

- Binary attachment helper command is not currently implemented in `acjr3`.
- Use Jira UI or another multipart-capable tool for large file upload, then reference artifact location in a comment.
