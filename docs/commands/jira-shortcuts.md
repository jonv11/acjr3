# Jira Shortcut Commands

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Request Command](request.md)

These commands are wrappers around fixed Jira REST API v3 paths.

## Issue

```bash
acjr3 issue create --project <KEY> --summary <TEXT> [--type <TYPE>] [--description <TEXT>] [--assignee <ACCOUNT_ID>] [--update-history true|false] [--raw] [--fail-on-non-success]
acjr3 issue create --body <JSON> | --body-file <PATH> [--raw] [--fail-on-non-success]
acjr3 issue update <KEY> [--summary <TEXT>] [--description <TEXT>] [--assignee <ACCOUNT_ID>] [--type <TYPE>] [--project <KEY>] [--raw] [--fail-on-non-success]
acjr3 issue update <KEY> --body <JSON> | --body-file <PATH> [--raw] [--fail-on-non-success]
acjr3 issue delete <KEY> [--delete-subtasks true|false] [--fail-on-non-success]
acjr3 issue view <KEY> [--fields <CSV>] [--fields-by-keys true|false] [--expand <CSV>] [--properties <CSV>] [--update-history true|false] [--fail-fast true|false] [--raw] [--fail-on-non-success] [--verbose]
acjr3 issue comment <KEY> --text <TEXT> [--raw] [--fail-on-non-success]
acjr3 issue comment add <KEY> [--text <TEXT> | --body <JSON> | --body-file <PATH>] [--expand <CSV>] [--raw] [--fail-on-non-success]
acjr3 issue comment list <KEY> [--start-at <N>] [--max-results <N>] [--order-by <TEXT>] [--expand <CSV>] [--raw] [--fail-on-non-success]
acjr3 issue comment get <KEY> <COMMENT_ID> [--expand <CSV>] [--raw] [--fail-on-non-success]
acjr3 issue comment update <KEY> <COMMENT_ID> [--text <TEXT> | --body <JSON> | --body-file <PATH>] [--notify-users true|false] [--override-editable-flag true|false] [--expand <CSV>] [--raw] [--fail-on-non-success]
acjr3 issue comment delete <KEY> <COMMENT_ID> [--fail-on-non-success]
acjr3 issue transition <KEY> [--body <JSON> | --body-file <PATH>] | [--id <TRANSITION_ID> | --to <TRANSITION_NAME>] [--raw] [--fail-on-non-success]
acjr3 issue transition list <KEY> [--expand <CSV>] [--transition-id <ID>] [--skip-remote-only-condition true|false] [--include-unavailable-transitions true|false] [--sort-by-ops-bar-and-status true|false] [--raw] [--fail-on-non-success]
acjr3 issue createmeta [--project-ids <CSV>] [--project-keys <CSV>] [--issuetype-ids <CSV>] [--issuetype-names <CSV>] [--expand <CSV>] [--raw] [--fail-on-non-success]
acjr3 issue editmeta <KEY> [--override-screen-security true|false] [--override-editable-flag true|false] [--raw] [--fail-on-non-success]
```

Notes:
- `issue create` maps to `POST /rest/api/3/issue`.
- `issue update` maps to `PUT /rest/api/3/issue/{issueIdOrKey}`.
- `issue delete` maps to `DELETE /rest/api/3/issue/{issueIdOrKey}`.
- `issue create` supports Jira `updateHistory` query parameter via `--update-history`.
- `issue update` supports Jira query parameters `notifyUsers`, `overrideScreenSecurity`, `overrideEditableFlag`, and `returnIssue`.
- `issue delete` supports Jira `deleteSubtasks` query parameter.
- `issue view --fields` maps to Jira `fields` query param.
- `issue transition --to` calls `GET /rest/api/3/issue/{key}/transitions` to resolve the name, then submits `POST` by transition ID.
- `issue comment` supports both legacy add form (`issue comment <KEY> --text`) and CRUD subcommands.
- `issue comment --text` generates Atlassian Document Format (ADF) comment payload automatically.
- `issue transition --to` resolves transition name to ID from available transitions, then submits by ID.

## Search

```bash
acjr3 search list [--project <KEY>] [--status <STATUS>] [--assignee <USER>] [--jql <JQL>] [--jql-file <PATH>] [--fields <CSV>] [--max-results <N>] [--next-page-token <TOKEN>] [--raw] [--fail-on-non-success] [--verbose]
```

Notes:
- If multiple filters are provided, they are combined with `AND`.
- Uses active v3 endpoint `GET /rest/api/3/search/jql`.
- `maxResults` defaults to `50` unless overridden.
- If `--jql` and `--jql-file` are both provided, they are combined with `AND` along with shortcut filters.

## Project

```bash
acjr3 project list [--start-at <N>] [--max-results <N>] [--order-by <TEXT>] [--id <CSV>] [--keys <CSV>] [--query <TEXT>] [--type-key <TEXT>] [--category-id <N>] [--action <TEXT>] [--expand <CSV>] [--status <CSV>] [--properties <CSV>] [--property-query <TEXT>] [--raw] [--fail-on-non-success] [--verbose]
acjr3 project component list --project <KEY> [--raw] [--fail-on-non-success] [--verbose]
acjr3 project version list --project <KEY> [--start-at <N>] [--max-results <N>] [--order-by <TEXT>] [--query <TEXT>] [--status <TEXT>] [--expand <CSV>] [--raw] [--fail-on-non-success] [--verbose]
```

## Other list commands

```bash
acjr3 priority list [--raw] [--fail-on-non-success] [--verbose]
acjr3 status list [--raw] [--fail-on-non-success] [--verbose]
acjr3 issuetype list [--raw] [--fail-on-non-success] [--verbose]
acjr3 user search [--query <TEXT>] [--username <TEXT>] [--account-id <ID>] [--start-at <N>] [--max-results <N>] [--property <KEY=VALUE>] [--raw] [--fail-on-non-success] [--verbose]
acjr3 field list [--raw] [--fail-on-non-success] [--verbose]
acjr3 field search [--start-at <N>] [--max-results <N>] [--type <TEXT>] [--id <CSV>] [--query <TEXT>] [--order-by <TEXT>] [--expand <CSV>] [--project-ids <CSV>] [--raw] [--fail-on-non-success] [--verbose]
acjr3 group list [--raw] [--fail-on-non-success] [--verbose]
acjr3 role list [--raw] [--fail-on-non-success] [--verbose]
acjr3 resolution list [--raw] [--fail-on-non-success] [--verbose]
```

Additional endpoint notes:
- `group list` maps to `GET /rest/api/3/group/bulk` and supports `--start-at`, `--max-results`, `--group-id`, `--group-name`, `--access-type`, `--application-key`.
- `resolution list` maps to `GET /rest/api/3/resolution/search` and supports `--start-at`, `--max-results`, `--id`, `--only-default true|false`.

Global config override options:
- All commands support runtime overrides: `--site-url`, `--auth-mode`, `--email`, `--api-token`, `--bearer-token`, `--timeout-seconds`, `--max-retries`, `--retry-base-delay-ms`.
- These overrides apply for the current invocation only.
