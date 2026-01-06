using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
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

        // both implementations of fns-to-refactor need --cache-path argument added later, when cli cache is going to be implemented
        public string GetRefactorCommandWithCodeSmells(string fileName, string codeSmells, string preflight = null)
        {
            var args = new List<string> { "refactor", "fns-to-refactor", "--file-name", fileName };
            
            if (!string.IsNullOrWhiteSpace(preflight))
            {
                args.Add("--preflight");
                args.Add(preflight);
            }
            
            args.Add("--code-smells");
            args.Add(codeSmells);
            
            return GetArgumentStr(args.ToArray());
        }

        // this implementation needs update of --extension to --file-name
        public string GetRefactorCommandWithDeltaResult(string extension, string deltaResult, string preflight = null)
        {
            var args = new List<string> { "refactor", "fns-to-refactor", "--extension", extension };
            
            if (!string.IsNullOrWhiteSpace(preflight))
            {
                args.Add("--preflight");
                args.Add(preflight);
            }
            
            args.Add("--delta-result");
            args.Add(deltaResult);
            
            return GetArgumentStr(args.ToArray());
        }

        public string GetRefactorPostCommand(FnToRefactorModel fnToRefactor, bool skipCache, string token = null)
        {
            var args = new List<string> { "refactor", "post" };
            if (skipCache)
                args.Add("--skip-cache");
            if (!string.IsNullOrWhiteSpace(token))
            {
                args.Add("--token");
                args.Add(token);
            }
           
            if (!string.IsNullOrEmpty(fnToRefactor.NippyB64))
            {
                args.Add("--fn-to-refactor-nippy-b64");
                args.Add(fnToRefactor.NippyB64);
            } 
            else
            {
				args.Add("--fn-to-refactor");
                args.Add(JsonConvert.SerializeObject(fnToRefactor));
			}
            var command = GetArgumentStr(args.ToArray());
            return command;
        }

        static string GetArgumentStr(params string[] args)
        {
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                PasteArguments.AppendArgument(sb, arg);
            }
            return sb.ToString();
        }

        public string GetReviewFileContentCommand(string path) => GetArgumentStr("review", "--file-name", path);

        public string GetReviewPathCommand(string path) => GetArgumentStr("review", path);

        public string GetReviewDeltaCommand(string oldScore, string newScore)
        {
            var scores = _creator.Create(oldScore, newScore);
            return $"{scores}";
        }
    }
}
