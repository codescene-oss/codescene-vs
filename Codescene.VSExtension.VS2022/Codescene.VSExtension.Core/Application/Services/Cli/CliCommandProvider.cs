using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliCommandProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliCommandProvider : ICliCommandProvider
    {
        public string VersionCommand => "version --sha";

        public string GetPreflightSupportInformationCommand(bool force)
        {
            var useForce = force ? " --force" : string.Empty;
            return $"refactor preflight{useForce}";
        }

        public string GetRefactorCommandWithCodeSmells(string extension, string codeSmellsJson)
            => $"refactor fns-to-refactor --extension {extension} --code-smells {codeSmellsJson}";

        public string GetReviewFileContentCommand(string path) => $"review --file-name {path}";
        public string GetReviewPathCommand(string path) => $"review {path}";
    }
}
