# 6. Incident Or Bug Reproduction And Escalation

## Goal

Capture reproducible evidence quickly, update severity/impact consistently, and escalate when needed.

## Commands used

- `acjr3 issue view`
- `acjr3 issue update`
- `acjr3 issue comment`
- `acjr3 request` (for links)
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

3. Apply bug template update (`Steps / Expected / Actual / Env / Regression`).

```bash
acjr3 issue update ACJ-333 --body-file bug-evidence-update.json --fail-on-non-success
```

4. Request missing artifacts and confirm repro.

```bash
acjr3 issue comment ACJ-333 --text "Repro confirmed on staging. Please attach full log bundle and timestamp for the failing request."
```

5. Add duplicate or causal link (via request API).

```bash
acjr3 request POST /rest/api/3/issueLink --body-file bug-link.json --fail-on-non-success
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
