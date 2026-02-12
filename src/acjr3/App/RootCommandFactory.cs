using System.CommandLine;
using Acjr3.Commands.Core;

namespace Acjr3.App;

public static class RootCommandFactory
{
    public static RootCommand Build(IServiceProvider services)
    {
        var root = new RootCommand("acjr3 - Atlassian Cloud Jira REST API v3 proxy CLI.");

        AddCommand(root, RequestCommandBuilder.Build(services), includeRuntimeOverrides: true);
        AddCommand(root, ConfigCommandBuilder.Build(), includeRuntimeOverrides: false);
        AddCommand(root, OpenApiCommandBuilder.Build(services), includeRuntimeOverrides: true);

        AddCommand(root, IssueCommands.BuildIssueCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, PriorityCommands.BuildPriorityCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, SearchCommands.BuildSearchCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, StatusCommands.BuildStatusCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, ProjectCommands.BuildProjectCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, IssueTypeCommands.BuildIssueTypeCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, IssueLinkCommands.BuildIssueLinkCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, UserCommands.BuildUserCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, FieldCommands.BuildFieldCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, GroupCommands.BuildGroupCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, RoleCommands.BuildRoleCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, ResolutionCommands.BuildResolutionCommand(services), includeRuntimeOverrides: true);

        AddCommand(root, AgentCommandBuilder.BuildCapabilitiesCommand(), includeRuntimeOverrides: true);
        AddCommand(root, AgentCommandBuilder.BuildSchemaCommand(), includeRuntimeOverrides: true);
        AddCommand(root, AgentCommandBuilder.BuildDoctorCommand(services), includeRuntimeOverrides: true);
        AddCommand(root, AgentCommandBuilder.BuildAuthCommand(), includeRuntimeOverrides: true);

        return root;
    }

    private static void AddCommand(RootCommand root, Command command, bool includeRuntimeOverrides)
    {
        if (includeRuntimeOverrides)
        {
            RuntimeOverrideOptions.AddRuntimeOverrideOptions(command);
        }

        OutputOptionBinding.AddGlobalOptions(command);
        root.AddCommand(command);
    }
}
