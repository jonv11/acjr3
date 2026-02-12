using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace Acjr3.Commands.Jira;

public static partial class IssueCommands
{
    private enum DescriptionFileFormat
    {
        Text,
        Adf
    }

    private enum FieldFileFormat
    {
        Json,
        Adf
    }

    public static Command BuildIssueCommand(IServiceProvider services)
    {
        var issue = new Command("issue", "Jira issue commands");
        issue.AddCommand(BuildCreateCommand(services));
        issue.AddCommand(BuildUpdateCommand(services));
        issue.AddCommand(BuildDeleteCommand(services));
        issue.AddCommand(BuildViewCommand(services));
        issue.AddCommand(BuildCommentCommand(services));
        issue.AddCommand(BuildTransitionCommand(services));
        issue.AddCommand(BuildCreateMetaCommand(services));
        issue.AddCommand(BuildEditMetaCommand(services));
        return issue;
    }
}
