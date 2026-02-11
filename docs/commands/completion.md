# Shell Completion

Navigation: [Docs Home](../README.md) | [Commands Index](README.md) | [Getting Started](../getting-started.md)

`acjr3` uses System.CommandLine completion via the `suggest` directive.

## Manual completion query

```bash
acjr3 "[suggest]"
```

This prints current top-level completions.

## Bash completion example

Add this function to your shell profile:

```bash
_acjr3_complete() {
  local IFS=$'\n'
  local suggestions
  suggestions=$(acjr3 "[suggest:${COMP_CWORD}]" "${COMP_WORDS[@]:1}")
  COMPREPLY=($(compgen -W "$suggestions" -- "${COMP_WORDS[COMP_CWORD]}"))
}
complete -F _acjr3_complete acjr3
```

## PowerShell completion example

Add this to your PowerShell profile:

```powershell
Register-ArgumentCompleter -Native -CommandName acjr3 -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $tokens = $commandAst.CommandElements | ForEach-Object { $_.Extent.Text }
    $index = [Math]::Max(0, $tokens.Count - 1)
    $tail = if ($tokens.Count -gt 1) { $tokens[1..($tokens.Count - 1)] } else { @() }
    $args = @("[suggest:$index]") + $tail
    acjr3 @args | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
    }
}
```

Notes:
- Completion behavior depends on shell tokenization and quoting.
- If a profile function is not enough for your shell setup, use explicit command aliases/wrappers and call the same `suggest` directive pattern.
