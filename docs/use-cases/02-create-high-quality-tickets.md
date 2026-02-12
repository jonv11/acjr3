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

1. Prepare the issue description in ADF (`create-issue-description.adf.json`).

```json
{
  "type": "doc",
  "version": 1,
  "content": [
    {
      "type": "paragraph",
      "content": [
        { "type": "text", "text": "Context: ..." }
      ]
    }
  ]
}
```

2. Create the issue.

```bash
acjr3 issue create ACJ \
  --type Story \
  --summary "Add retry-safe import endpoint" \
  --description-file create-issue-description.adf.json --description-format adf \
  --yes \
  --fail-on-non-success --compact
```

3. Enrich with planning fields (priority, points, fixVersion, custom fields).

```bash
acjr3 issue update ACJ-456 --in enrich-issue.json --input-format json --yes --fail-on-non-success
```

4. Link dependencies.

```bash
acjr3 issuelink --type "Blocks" --inward "ACJ-456" --outward "ACJ-320" \
  --yes \
  --fail-on-non-success
```

Use `--in <PATH> --input-format json` when you need advanced fields beyond `--type`, `--inward`, and `--outward`.

5. Add follow-up open questions comment (ADF-first).

```bash
acjr3 issue comment add ACJ-456 --in open-questions-comment.adf.json --input-format adf --yes
```

6. Verify final issue state.

```bash
acjr3 issue view ACJ-456 --fields "summary,description,priority,labels,fixVersions" --compact
```

## Accuracy notes

- `issue attach` shortcut command is not currently implemented.
- For binary attachment upload, use Jira UI or another tool that supports multipart file upload.


