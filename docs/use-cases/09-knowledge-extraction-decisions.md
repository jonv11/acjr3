# 9. Knowledge Extraction: Decisions And Rationale

Navigation: [Use Cases Index](README.md) | [Docs Home](../README.md) | [Commands](../commands/README.md) | [Previous](08-release-coordination-changelog.md) | [Next](10-bulk-maintenance-hygiene.md)

## Goal

Compress long issue history into a canonical decision summary and connect it to documentation work.

## Commands used

- `acjr3 issue view`
- `acjr3 issue comment list`
- `acjr3 issue comment`
- `acjr3 issue update`
- `acjr3 issuelink`

## Step-by-step

1. Pull issue with context and linked items.

```bash
acjr3 issue view ACJ-620 --fields "summary,description,issuelinks,labels,status" --compact
acjr3 issue comment list ACJ-620 --max-results 100 --compact
```

2. Detect decision signal.
- explicit approval in comments
- final accepted approach chosen
- alternative options rejected with rationale

3. Post structured decision summary comment (ADF-first).

```bash
acjr3 issue comment add ACJ-620 --text-file decision-summary-comment.adf.json --yes
```

4. Update canonical description (ADF) and decision label.

```bash
acjr3 issue update ACJ-620 --field description --field-file decision-canonical-description.adf.json --field-format adf --yes --allow-non-success
acjr3 issue update ACJ-620 --in decision-captured-update.json --yes --allow-non-success
```

5. Link documentation or ADR task (if required).

```bash
acjr3 issuelink --type "Relates" --inward "ACJ-620" --outward "ACJ-621" --yes --allow-non-success
```

6. Verify final state.

```bash
acjr3 issue view ACJ-620 --fields "description,labels,issuelinks" --compact
```


