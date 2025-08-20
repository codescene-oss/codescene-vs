using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using MediaBrowser.Model.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliCommandProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliCommandProvider : ICliCommandProvider
    {
        [Import]
        private readonly ICliObjectScoreCreator _creator;

        public string VersionCommand => "version --sha";

        public string DeviceIdCommand => "telemetry --device-id";

        public string SendTelemetryCommand(string jsonEvent) => $"telemetry --event \"{AdjustTelemetryQuotes(jsonEvent)}\"";

        public string GetPreflightSupportInformationCommand(bool force)
        {
            var useForceArg = force ? " --force" : string.Empty;
            return $"refactor preflight{useForceArg}";
        }

        private string AdjustTelemetryQuotes(string value) => value.Replace("\"", "\\\"");
        private string AdjustQuotes(string value) => value.Replace("\"", "\"\"");

        public string GetRefactorCommandWithCodeSmells(string extension, string codeSmells, string preflight = null)
        {
            var preflightArg = string.IsNullOrWhiteSpace(preflight) ? string.Empty : $" --preflight \"{AdjustQuotes(preflight)}\"";
            return $"refactor fns-to-refactor --extension {extension}{preflightArg} --code-smells \"{AdjustQuotes(codeSmells)}\"";
        }

        public string GetRefactorCommandWithDeltaResult(string extension, string deltaResult, string preflight = null)
        {
            var preflightArg = string.IsNullOrWhiteSpace(preflight) ? string.Empty : $" --preflight \"{AdjustQuotes(preflight)}\"";
            return $"refactor fns-to-refactor --extension {extension}{preflightArg} --delta-result {deltaResult}";
        }

        public string GetRefactorPostCommand(string fnToRefactor, bool skipCache, bool useStagingApi = false, string token = null)
        {
            var args = new List<string> { "refactor", "post" };
            if (skipCache)
                args.Add("--skip-cache");
            if (useStagingApi)
                args.Add("--staging");
            if (!string.IsNullOrWhiteSpace(token))
            {
                args.Add($"--token");
                args.Add(token);
            }
            args.Add($"--fn-to-refactor");
            args.Add(fnToRefactor);
            var command = GetArgumentStr(args.ToArray());
            return command;
        }

        static string GetArgumentStr(params string[] args)
        {
            var sb = new ValueStringBuilder();
            foreach (var arg in args)
            {
                PasteArguments.AppendArgument(ref sb, arg);
            }
            return sb.ToString();
        }

        public string GetReviewFileContentCommand(string path) => $"review --file-name {path}";

        public string GetReviewPathCommand(string path) => $"review {path}";

        public string GetReviewDeltaCommand(string oldScore, string newScore)
        {
            var scores = _creator.Create(oldScore, newScore);
            return $"{scores}";
        }
    }
}
