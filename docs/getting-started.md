# Getting Started

Navigation: [Docs Home](README.md) | [Configuration](configuration.md) | [Commands](commands/README.md)

## Requirements

- .NET 8 SDK
- Jira Cloud site URL (for example `https://your-domain.atlassian.net`)
- Jira credentials for one auth mode (`basic` or `bearer`)
- For `basic`: email + API token
- For `bearer`: bearer token

## Build and test

```bash
dotnet build acjr3.sln
dotnet test acjr3.sln
```

## First command

```bash
dotnet run --project src/acjr3 -- --help
```

## Minimal setup check

After setting environment variables, run:

```bash
dotnet run --project src/acjr3 -- config check
```

## Build distributable binary

Example (Linux x64):

```bash
dotnet publish src/acjr3/acjr3.csproj -c Release -p:PublishProfile=SingleFileSelfContained -r linux-x64 --self-contained true
```

Change runtime identifier (`-r`) as needed, for example `win-x64` or `osx-x64`.
