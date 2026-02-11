# 2. Create High-quality Tickets From Chat Or Spec Fragments

## Goal

Convert unstructured text into a complete Jira issue with consistent fields and explicit acceptance criteria.

## Commands used

- `acjr3 issue create`
- `acjr3 issue update`
- `acjr3 request` (for issue links)
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

4. Link dependencies (shortcut does not exist; use `request`).

```bash
acjr3 request POST /rest/api/3/issueLink \
  --body-file link-blocks.json \
  --fail-on-non-success
```

`link-blocks.json` example:

```json
{
  "type": { "name": "Blocks" },
  "inwardIssue": { "key": "ACJ-456" },
  "outwardIssue": { "key": "ACJ-320" }
}
```

5. Add follow-up open questions comment.

```bash
acjr3 issue comment ACJ-456 --text "Open questions: 1) final timeout policy? 2) migration strategy? 3) rollout flag owner?"
```

6. Verify final issue state.

```bash
acjr3 issue view ACJ-456 --fields "summary,description,priority,labels,fixVersions" --raw
```

## Accuracy notes

- `issue link` and `issue attach` shortcut commands are not currently implemented.
- For linking, use `request` with `/rest/api/3/issueLink`.
- For binary attachment upload, use Jira UI or another tool that supports multipart file upload.
