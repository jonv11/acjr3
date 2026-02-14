using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Acjr3.App;

namespace Acjr3.Tests.Integration;

[Collection("ProgramE2e")]
public sealed partial class ProgramE2eTests
{
    [Fact]
    public async Task RequestCommand_Success_EmitsEnvelope()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "GET", "/rest/api/3/project",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"ok\": true", stdout);
    }

    [Fact]
    public async Task RequestCommand_PostWithoutYes_FailsValidation()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"name\":\"x\"}");
        try
        {
            var args = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--in", bodyFile
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("requires --yes or --force", stdout);
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task RequestCommand_PostWithoutExplicitBody_UsesDefaultJsonObject()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "POST", "/rest/api/3/project",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--yes"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        using var body = JsonDocument.Parse(server.LastRequest!.Body);
        Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
        Assert.Empty(body.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task RequestCommand_InJsonPayload_DefaultsContentTypeJson()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var payloadFile = WriteTempFile("{\"name\":\"from-in\"}");
        try
        {
            var args = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--in", payloadFile,
                "--yes"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            Assert.True(server.LastRequest!.Headers.TryGetValue("Content-Type", out var contentType));
            Assert.StartsWith("application/json", contentType, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(payloadFile);
        }
    }

    [Fact]
    public async Task RequestCommand_InNonObjectPayload_FailsValidation()
    {
        var payloadFile = WriteTempFile("[1,2,3]");
        try
        {
            var args = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--in", payloadFile,
                "--yes"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("--in payload must be a JSON object.", stdout);
        }
        finally
        {
            File.Delete(payloadFile);
        }
    }

    [Fact]
    public async Task RequestCommand_RemovedBodyOption_FailsUnknownOption()
    {
        var args = new[]
        {
            "request", "POST", "/rest/api/3/project",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--body", "{\"a\":1}",
            "--yes"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--body", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestCommand_RemovedFailOnNonSuccessOption_FailsUnknownOption()
    {
        var args = new[]
        {
            "request", "GET", "/rest/api/3/project",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--fail-on-non-success"
        };

        var (exitCode, stdout, stderr) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.True(
            stdout.Contains("--fail-on-non-success", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("--fail-on-non-success", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RequestCommand_NonSuccess_DefaultFails()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            404,
            "Not Found",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"error\":\"missing\"}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "GET", "/rest/api/3/project/ACJ",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task RequestCommand_NonSuccess_WithAllowNonSuccess_Succeeds()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            404,
            "Not Found",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"error\":\"missing\"}"));
        await server.StartAsync();

        var args = new[]
        {
            "request", "GET", "/rest/api/3/project/ACJ",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--allow-non-success"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task IssueCreate_WithoutYes_FailsValidation()
    {
        var args = new[]
        {
            "issue", "create", "ACJ", "--summary", "Needs confirmation",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("requires --yes or --force", stdout);
    }

    [Fact]
    public async Task IssueCreate_RemovedBodyFileOption_FailsUnknownOption()
    {
        var args = new[]
        {
            "issue", "create", "ACJ",
            "--summary", "Needs confirmation",
            "--body-file", "payload.json",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, stderr) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.True(
            stdout.Contains("--body-file", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("--body-file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IssueCreate_RemovedFailOnNonSuccessOption_FailsUnknownOption()
    {
        var args = new[]
        {
            "issue", "create", "ACJ",
            "--summary", "Needs confirmation",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--fail-on-non-success"
        };

        var (exitCode, stdout, stderr) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.True(
            stdout.Contains("--fail-on-non-success", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("--fail-on-non-success", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IssueCreate_MissingRequiredFields_FailsValidation()
    {
        var args = new[]
        {
            "issue", "create",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("fields.project.key", stdout);
    }

    [Fact]
    public async Task IssueUpdate_WithoutChanges_FailsValidation()
    {
        var args = new[]
        {
            "issue", "update", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("at least one issue update operation", stdout);
    }

    [Fact]
    public async Task IssueTransition_WithoutTransitionId_FailsValidation()
    {
        var args = new[]
        {
            "issue", "transition", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("transition.id", stdout);
    }

    [Fact]
    public async Task IssueDelete_WithoutYesOrForce_FailsValidation()
    {
        var args = new[]
        {
            "issue", "delete", "ACJ-1",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("requires --yes or --force", stdout);
    }

    [Fact]
    public async Task IssueDelete_WithDeleteSubtasksTrue_MapsBooleanQuery()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            204,
            "No Content",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            string.Empty));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "delete", "ACJ-1",
            "--delete-subtasks", "true",
            "--yes",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        Assert.Equal("DELETE", server.LastRequest!.Method);

        var requestUri = BuildLastRequestUri(server);
        var query = ParseQuery(requestUri.Query);
        Assert.Equal("/rest/api/3/issue/ACJ-1", requestUri.AbsolutePath);
        Assert.Equal("true", query["deleteSubtasks"]);
    }

    [Fact]
    public async Task IssueDelete_InvalidDeleteSubtasksValue_FailsValidation()
    {
        var args = new[]
        {
            "issue", "delete", "ACJ-1",
            "--delete-subtasks", "maybe",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--delete-subtasks must be", stdout);
        Assert.Contains("true", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("false", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueTransition_WithToAndIdTogether_FailsValidation()
    {
        var args = new[]
        {
            "issue", "transition", "ACJ-1",
            "--to", "Done",
            "--id", "31",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("Provide either --to or --id, not both.", stdout);
    }

    [Fact]
    public async Task IssueTransitionList_InvalidBooleanOption_FailsValidation()
    {
        var args = new[]
        {
            "issue", "transition", "list", "ACJ-1",
            "--skip-remote-only-condition", "yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--skip-remote-only-condition must be", stdout);
        Assert.Contains("true", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("false", stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueTransitionList_WithBooleanOptions_MapsExpectedQuery()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"transitions\":[]}"));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "transition", "list", "ACJ-123",
            "--expand", "transitions.fields",
            "--transition-id", "41",
            "--skip-remote-only-condition", "true",
            "--include-unavailable-transitions", "false",
            "--sort-by-ops-bar-and-status", "true",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        Assert.Equal("GET", server.LastRequest!.Method);

        var requestUri = BuildLastRequestUri(server);
        var query = ParseQuery(requestUri.Query);
        Assert.Equal("/rest/api/3/issue/ACJ-123/transitions", requestUri.AbsolutePath);
        Assert.Equal("transitions.fields", query["expand"]);
        Assert.Equal("41", query["transitionId"]);
        Assert.Equal("true", query["skipRemoteOnlyCondition"]);
        Assert.Equal("false", query["includeUnavailableTransitions"]);
        Assert.Equal("true", query["sortByOpsBarAndStatus"]);
    }

    [Fact]
    public async Task IssueTransition_WithTo_ResolvesTransitionNameThenPostsTransitionId()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"transitions\":[{\"id\":\"41\",\"name\":\"Done\"}]}"));
        server.EnqueueResponse(new ReplayResponse(
            204,
            "No Content",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            string.Empty));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "transition", "ACJ-123",
            "--to", "Done",
            "--yes",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        Assert.Equal("POST", server.LastRequest!.Method);
        Assert.StartsWith("/rest/api/3/issue/ACJ-123/transitions", server.LastRequest.Path, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(server.LastRequest.Body);
        Assert.Equal("41", body.RootElement.GetProperty("transition").GetProperty("id").GetString());
    }

    [Fact]
    public async Task IssueCommentAdd_WithoutBody_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "add", "ACJ-1",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("non-empty body", stdout);
    }

    [Fact]
    public async Task IssueLink_WithoutRequiredFields_FailsValidation()
    {
        var args = new[]
        {
            "issuelink",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("type.name", stdout);
    }

    [Fact]
    public async Task JiraShortcut_MissingAuth_MapsToAuthenticationExitCode()
    {
        var args = new[]
        {
            "issue", "view", "ACJ-1",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", string.Empty
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal((int)CliExitCode.Authentication, exitCode);
        Assert.Contains("authentication_error", stdout);
    }

    [Fact]
    public async Task IssueCreate_InBase_AllowsSummarySugarOverride()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"fields\":{\"project\":{\"key\":\"ACJ\"},\"summary\":\"from-body\",\"issuetype\":{\"name\":\"Task\"}}}");
        try
        {
            var args = new[]
            {
                "issue", "create",
                "--in", bodyFile,
                "--summary", "from-sugar",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("from-sugar", body.RootElement.GetProperty("fields").GetProperty("summary").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_WithInJsonPayloadAndYes_Succeeds()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"body\":{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"from-in-payload\"}]}]}}");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-123",
                "--in", bodyFile,
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"id\": \"10001\"", stdout);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("from-in-payload", body.RootElement.GetProperty("body").GetProperty("content")[0].GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_WithTextFileAdf_SetsBodyFromFile()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var textFile = WriteTempFile("{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"from-adf-file\"}]}]}");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-123",
                "--text-file", textFile,
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("doc", body.RootElement.GetProperty("body").GetProperty("type").GetString());
            Assert.Equal("from-adf-file", body.RootElement.GetProperty("body").GetProperty("content")[0].GetProperty("content")[0].GetProperty("text").GetString());
        }
        finally
        {
            File.Delete(textFile);
        }
    }

    [Fact]
    public async Task IssueCommentUpdate_WithTextFileJson_SetsBodyFromFile()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var textFile = WriteTempFile("123");
        try
        {
            var args = new[]
            {
                "issue", "comment", "update", "ACJ-123", "10001",
                "--text-file", textFile,
                "--text-format", "json",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal(123, body.RootElement.GetProperty("body").GetInt32());
        }
        finally
        {
            File.Delete(textFile);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_TextAndTextFileTogether_FailsValidation()
    {
        var textFile = WriteTempFile("{\"type\":\"doc\",\"version\":1,\"content\":[]}");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-1",
                "--text", "inline text",
                "--text-file", textFile,
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("Use either --text or --text-file, not both.", stdout);
        }
        finally
        {
            File.Delete(textFile);
        }
    }

    [Fact]
    public async Task IssueCommentUpdate_TextFormatWithoutTextFile_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "update", "ACJ-1", "10001",
            "--text-format", "adf",
            "--yes",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--text-format requires --text-file.", stdout);
    }

    [Fact]
    public async Task IssueCommentAdd_TextFormatInvalid_FailsValidation()
    {
        var textFile = WriteTempFile("{\"type\":\"doc\",\"version\":1,\"content\":[]}");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-1",
                "--text-file", textFile,
                "--text-format", "yaml",
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("--text-format must be one of: json, adf.", stdout);
        }
        finally
        {
            File.Delete(textFile);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_TextFileAdfRequiresObject_FailsValidation()
    {
        var textFile = WriteTempFile("\"not-an-object\"");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-1",
                "--text-file", textFile,
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("must contain an ADF document object", stdout);
        }
        finally
        {
            File.Delete(textFile);
        }
    }

    [Fact]
    public async Task HelpOutput_CommentCommands_IncludeTextFileOptions()
    {
        foreach (var commandLine in new[] { "issue comment add --help", "issue comment update --help" })
        {
            var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.Contains("--text-file", stdout);
            Assert.Contains("--text-format", stdout);
        }
    }

    [Fact]
    public async Task IssueCommentAdd_WithInBareAdfBody_FailsValidation()
    {
        var bodyFile = WriteTempFile("{\"type\":\"doc\",\"version\":1,\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"no-body-wrapper\"}]}]}");
        try
        {
            var args = new[]
            {
                "issue", "comment", "add", "ACJ-1",
                "--in", bodyFile,
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("Final payload must include a non-empty body.", stdout);
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueCommentList_WithPaginationOptions_MapsQuery()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"values\":[]}"));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "comment", "list", "ACJ-123",
            "--start-at", "2",
            "--max-results", "5",
            "--order-by", "-created",
            "--expand", "renderedBody",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        Assert.Equal("GET", server.LastRequest!.Method);

        var requestUri = BuildLastRequestUri(server);
        var query = ParseQuery(requestUri.Query);
        Assert.Equal("/rest/api/3/issue/ACJ-123/comment", requestUri.AbsolutePath);
        Assert.Equal("2", query["startAt"]);
        Assert.Equal("5", query["maxResults"]);
        Assert.Equal("-created", query["orderBy"]);
        Assert.Equal("renderedBody", query["expand"]);
    }

    [Fact]
    public async Task IssueCommentList_InvalidStartAt_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "list", "ACJ-123",
            "--start-at", "-1",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--start-at must be zero or greater.", stdout);
    }

    [Fact]
    public async Task IssueCommentList_InvalidMaxResults_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "list", "ACJ-123",
            "--max-results", "0",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("--max-results must be greater than zero.", stdout);
    }

    [Fact]
    public async Task IssueCommentGet_WithExpand_MapsPathAndQuery()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            200,
            "OK",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "comment", "get", "ACJ-123", "10001",
            "--expand", "renderedBody",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.NotNull(server.LastRequest);
        Assert.Equal("GET", server.LastRequest!.Method);

        var requestUri = BuildLastRequestUri(server);
        var query = ParseQuery(requestUri.Query);
        Assert.Equal("/rest/api/3/issue/ACJ-123/comment/10001", requestUri.AbsolutePath);
        Assert.Equal("renderedBody", query["expand"]);
    }

    [Fact]
    public async Task IssueCommentDelete_WithoutYesOrForce_FailsValidation()
    {
        var args = new[]
        {
            "issue", "comment", "delete", "ACJ-123", "10001",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(1, exitCode);
        Assert.Contains("requires --yes or --force", stdout);
    }

    [Fact]
    public async Task IssueCommentDelete_WithAllowNonSuccess_ReturnsZeroOn404()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            404,
            "Not Found",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"error\":\"missing\"}"));
        await server.StartAsync();

        var args = new[]
        {
            "issue", "comment", "delete", "ACJ-123", "10001",
            "--yes",
            "--allow-non-success",
            "--site-url", server.BaseUrl,
            "--auth-mode", "bearer",
            "--bearer-token", "token"
        };

        var (exitCode, _, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task IssueCreate_DescriptionFormatText_FailsValidation()
    {
        var descriptionFile = WriteTempFile("{\"type\":\"doc\",\"version\":1,\"content\":[]}");
        try
        {
            var args = new[]
            {
                "issue", "create", "ACJ",
                "--summary", "Validation check",
                "--description-file", descriptionFile,
                "--description-format", "text",
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("--description-format must be one of: json, adf.", stdout);
        }
        finally
        {
            File.Delete(descriptionFile);
        }
    }

    [Fact]
    public async Task IssueCreate_DescriptionFileDefaultAdf_RequiresAdfShape()
    {
        var descriptionFile = WriteTempFile("{\"foo\":\"bar\"}");
        try
        {
            var args = new[]
            {
                "issue", "create", "ACJ",
                "--summary", "Validation check",
                "--description-file", descriptionFile,
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("must contain an ADF document object", stdout);
        }
        finally
        {
            File.Delete(descriptionFile);
        }
    }

    [Fact]
    public async Task IssueCreate_DescriptionFileJson_AllowsArbitraryJson()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"id\":\"10001\"}"));
        await server.StartAsync();

        var descriptionFile = WriteTempFile("\"plain-description\"");
        try
        {
            var args = new[]
            {
                "issue", "create", "ACJ",
                "--summary", "Description from json file",
                "--description-file", descriptionFile,
                "--description-format", "json",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("plain-description", body.RootElement.GetProperty("fields").GetProperty("description").GetString());
        }
        finally
        {
            File.Delete(descriptionFile);
        }
    }

    [Fact]
    public async Task IssueUpdate_FieldFileDefaultAdf_RequiresAdfShape()
    {
        var fieldFile = WriteTempFile("{\"foo\":\"bar\"}");
        try
        {
            var args = new[]
            {
                "issue", "update", "ACJ-1",
                "--field", "description",
                "--field-file", fieldFile,
                "--yes",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, stdout, _) = await InvokeProgramAsync(args);
            Assert.Equal(1, exitCode);
            Assert.Contains("must contain an ADF document object", stdout);
        }
        finally
        {
            File.Delete(fieldFile);
        }
    }

    [Fact]
    public async Task IssueUpdate_FieldFileJson_AllowsArbitraryJson()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            204,
            "No Content",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            string.Empty));
        await server.StartAsync();

        var fieldFile = WriteTempFile("123");
        try
        {
            var args = new[]
            {
                "issue", "update", "ACJ-1",
                "--field", "customfield_123",
                "--field-file", fieldFile,
                "--field-format", "json",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal(123, body.RootElement.GetProperty("fields").GetProperty("customfield_123").GetInt32());
        }
        finally
        {
            File.Delete(fieldFile);
        }
    }

    [Fact]
    public async Task IssueTransition_InBase_AllowsIdSugarOverride()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            204,
            "No Content",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            string.Empty));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"transition\":{\"id\":\"11\"}}");
        try
        {
            var args = new[]
            {
                "issue", "transition", "ACJ-123",
                "--in", bodyFile,
                "--id", "31",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("31", body.RootElement.GetProperty("transition").GetProperty("id").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task IssueLink_InBase_AllowsSugarOverrides()
    {
        await using var server = new LocalReplayServer();
        server.EnqueueResponse(new ReplayResponse(
            201,
            "Created",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"ok\":true}"));
        await server.StartAsync();

        var bodyFile = WriteTempFile("{\"type\":{\"name\":\"Blocks\"},\"inwardIssue\":{\"key\":\"ACJ-1\"},\"outwardIssue\":{\"key\":\"ACJ-2\"}}");
        try
        {
            var args = new[]
            {
                "issuelink",
                "--in", bodyFile,
                "--outward", "ACJ-99",
                "--yes",
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token"
            };

            var (exitCode, _, _) = await InvokeProgramAsync(args);
            Assert.Equal(0, exitCode);
            Assert.NotNull(server.LastRequest);
            using var body = JsonDocument.Parse(server.LastRequest!.Body);
            Assert.Equal("ACJ-99", body.RootElement.GetProperty("outwardIssue").GetProperty("key").GetString());
            Assert.Equal("ACJ-1", body.RootElement.GetProperty("inwardIssue").GetProperty("key").GetString());
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    [Fact]
    public async Task RequestCommand_Explain_WritesRequestSummary()
    {
        var args = new[]
        {
            "request", "GET", "/rest/api/3/myself",
            "--site-url", "https://example.atlassian.net",
            "--auth-mode", "bearer",
            "--bearer-token", "token",
            "--query", "expand=groups",
            "--explain"
        };

        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"method\": \"GET\"", stdout);
        Assert.Contains("\"path\": \"/rest/api/3/myself\"", stdout);
    }

    [Fact]
    public async Task RequestCommand_Replay_UsesRecordedPayload()
    {
        var bodyFile = WriteTempFile("{\"name\":\"replay-test\"}");
        var replayFile = WriteTempFile(string.Empty);
        try
        {
            var explainArgs = new[]
            {
                "request", "POST", "/rest/api/3/project",
                "--site-url", "https://example.atlassian.net",
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--query", "expand=description",
                "--header", "X-Test=1",
                "--in", bodyFile,
                "--request-file", replayFile,
                "--explain",
                "--yes"
            };

            var (explainExitCode, _, _) = await InvokeProgramAsync(explainArgs);
            Assert.Equal(0, explainExitCode);

            var recorded = JsonSerializer.Deserialize<RecordedRequest>(File.ReadAllText(replayFile));
            Assert.NotNull(recorded);
            Assert.Equal("POST", recorded!.Method);
            Assert.Equal("/rest/api/3/project", recorded.Path);
            Assert.Contains(recorded.Query, q => q.Key == "expand" && q.Value == "description");
            Assert.Contains(recorded.Headers, h => h.Key == "X-Test" && h.Value == "1");
            Assert.Equal("{\"name\":\"replay-test\"}", recorded.Body);

            await using var server = new LocalReplayServer();
            server.EnqueueResponse(new ReplayResponse(
                200,
                "OK",
                new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                "{\"ok\":true}"));
            await server.StartAsync();

            var replayArgs = new[]
            {
                "request", "--replay", replayFile,
                "--site-url", server.BaseUrl,
                "--auth-mode", "bearer",
                "--bearer-token", "token",
                "--yes"
            };

            var (replayExitCode, stdout, _) = await InvokeProgramAsync(replayArgs);
            Assert.Equal(0, replayExitCode);
            Assert.Contains("\"Success\": true", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(bodyFile);
            File.Delete(replayFile);
        }
    }

    [Fact]
    public async Task CapabilitiesCommand_EmitsExitCodeContract()
    {
        var (exitCode, stdout, _) = await InvokeProgramAsync(["capabilities"]);
        Assert.Equal(0, exitCode);
        Assert.Contains("\"exitCodes\"", stdout);
        Assert.Contains("\"10\": \"Internal / tool-specific\"", stdout);
    }

    private static Uri BuildLastRequestUri(LocalReplayServer server)
    {
        Assert.NotNull(server.LastRequest);
        return new Uri(new Uri(server.BaseUrl), server.LastRequest!.Path);
    }

    private static Dictionary<string, string> ParseQuery(string rawQuery)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return parsed;
        }

        foreach (var pair in rawQuery.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0)
            {
                parsed[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            parsed[key] = value;
        }

        return parsed;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue comment add --help")]
    [InlineData("config init --help")]
    [InlineData("openapi --help")]
    [InlineData("doctor --help")]
    [InlineData("schema --help")]
    public async Task HelpOutput_HasNoDuplicateOptionRows(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);

        var optionRows = Regex.Matches(stdout, @"^\s{2,}--[a-z0-9-]+", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Select(m => m.Value.Trim())
            .ToList();

        var duplicate = optionRows
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        Assert.True(duplicate is null, $"Duplicate option row found: {duplicate?.Key}");
    }

    [Theory]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue transition --help")]
    [InlineData("issue comment add --help")]
    [InlineData("issue comment update --help")]
    [InlineData("issuelink --help")]
    public async Task HelpOutput_DoesNotIncludeRemovedPayloadOptions(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("--body", stdout);
        Assert.DoesNotContain("--body-file", stdout);
    }

    [Theory]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue transition --help")]
    [InlineData("issue comment add --help")]
    [InlineData("issue comment update --help")]
    [InlineData("issuelink --help")]
    public async Task HelpOutput_DoesNotIncludeInputFormat(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("--input-format", stdout);
    }

    [Theory]
    [InlineData("request --help")]
    [InlineData("issue create --help")]
    [InlineData("issue update --help")]
    [InlineData("issue transition --help")]
    [InlineData("issue comment add --help")]
    [InlineData("issue comment update --help")]
    [InlineData("issuelink --help")]
    public async Task HelpOutput_IncludesAllowNonSuccess(string commandLine)
    {
        var args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (exitCode, stdout, _) = await InvokeProgramAsync(args);
        Assert.Equal(0, exitCode);
        Assert.Contains("--allow-non-success", stdout);
    }

}
