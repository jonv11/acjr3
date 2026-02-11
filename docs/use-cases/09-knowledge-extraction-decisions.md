# 9. Knowledge Extraction: Decisions And Rationale

## Goal

Compress long issue history into a canonical decision summary and connect it to documentation work.

## Commands used

- `acjr3 issue view`
- `acjr3 issue comment list`
- `acjr3 issue comment`
- `acjr3 issue update`
- `acjr3 request` (for linking to doc task)

## Step-by-step

1. Pull issue with context and linked items.

```bash
acjr3 issue view ACJ-620 --fields "summary,description,issuelinks,labels,status" --raw
acjr3 issue comment list ACJ-620 --max-results 100 --raw
```

2. Detect decision signal.
- explicit approval in comments
- final accepted approach chosen
- alternative options rejected with rationale

3. Post structured decision summary comment.

```bash
acjr3 issue comment ACJ-620 --text "Decision: Use token-based pagination for search sync. Alternatives rejected: offset-only pagination due to consistency risk. Rationale: lower duplicate/skip probability under concurrent updates."
```

4. Update canonical description and decision label.

```bash
acjr3 issue update ACJ-620 --body-file decision-captured-update.json --fail-on-non-success
```

5. Link documentation or ADR task (if required).

```bash
acjr3 request POST /rest/api/3/issueLink --body-file decision-link-doc.json --fail-on-non-success
```

6. Verify final state.

```bash
acjr3 issue view ACJ-620 --fields "description,labels,issuelinks" --raw
```
