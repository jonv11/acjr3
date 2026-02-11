# 2. Create High-quality Tickets From Chat Or Spec Fragments

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](01-ticket-triage-inbox-processing.md) | [Next](03-refine-ready-for-dev.md)

## Goal

Convert unstructured text into a complete Jira issue with consistent fields and explicit acceptance criteria.

## Commands used

- `acjr3 issue create`
- `acjr3 issue update`
- `acjr3 issuelink`
- `acjr3 issue comment`

## Step-by-step

1. Prepare a structured template in `create-issue.json`.

```json
{
  "fields": {
    "project": { "key": "ACJ" },
    "issuetype": { "name": "Story" },
    "summary": "Add retry-safe import endpoint",
    "description": "Context:\n...\n\nGoal:\n...\n\nScope:\n...\n\nAcceptance Criteria:\n1. ...\n2. ...\n\nOut of scope:\n...\n\nRisks:\n..."
  }
}
```

2. Create the issue.

```bash
acjr3 issue create --body-file create-issue.json --fail-on-non-success --raw
```

3. Enrich with planning fields (priority, points, fixVersion, custom fields).

```bash
acjr3 issue update ACJ-456 --body-file enrich-issue.json --fail-on-non-success
```

4. Link dependencies.

```bash
acjr3 issuelink --type "Blocks" --inward "ACJ-456" --outward "ACJ-320" \
  --fail-on-non-success
```

Use `--body` or `--body-file` only when you need advanced fields beyond `--type`, `--inward`, and `--outward`.

5. Add follow-up open questions comment.

```bash
acjr3 issue comment ACJ-456 --text "Open questions: 1) final timeout policy? 2) migration strategy? 3) rollout flag owner?"
```

6. Verify final issue state.

```bash
acjr3 issue view ACJ-456 --fields "summary,description,priority,labels,fixVersions" --raw
```

## Accuracy notes

- `issue attach` shortcut command is not currently implemented.
- For binary attachment upload, use Jira UI or another tool that supports multipart file upload.
