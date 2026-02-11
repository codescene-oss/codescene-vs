// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliCommandProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliCommandProvider : ICliCommandProvider
    {
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore };

        private readonly ICliObjectScoreCreator _creator;

        [ImportingConstructor]
        public CliCommandProvider(ICliObjectScoreCreator creator)
        {
            _creator = creator;
        }

        public string VersionCommand => "version --sha";

        public string DeviceIdCommand => "telemetry --device-id";

        public string RefactorCommand => "run-command fns-to-refactor";

        public string ReviewFileContentCommand => "run-command review";

        public string SendTelemetryCommand(string jsonEvent) => $"telemetry --event \"{AdjustTelemetryQuotes(jsonEvent)}\"";

        public string GetPreflightSupportInformationCommand(bool force)
        {
            var useForceArg = force ? " --force" : string.Empty;
            return $"refactor preflight{useForceArg}";
        }

        public string GetRefactorWithCodeSmellsPayload(string fileName, string fileContent, string cachePath, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight = null)
        {
            var request = new FnsToRefactorCodeSmellRequestModel { CodeSmells = codeSmells };
            return SerializeRefactorRequest(request, fileName, fileContent, cachePath, preflight);
        }

        public string GetRefactorWithDeltaResultPayload(string fileName, string fileContent, string cachePath, DeltaResponseModel deltaResult, PreFlightResponseModel preflight = null)
        {
            var request = new FnsToRefactorDeltaRequestModel { DeltaResult = deltaResult };
            return SerializeRefactorRequest(request, fileName, fileContent, cachePath, preflight);
        }

        public string GetRefactorPostCommand(FnToRefactorModel fnToRefactor, bool skipCache, string token = null)
        {
            var args = new List<string> { "refactor", "post" };
            if (skipCache)
            {
                args.Add("--skip-cache");
            }

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

            var command = GetArgumentStr(args.ToArray());
            return command;
        }

        public string GetReviewFileContentPayload(string filePath, string fileContent, string cachePath)
        {
            var request = new ReviewRequestModel
            {
                FilePath = filePath,
                FileContent = fileContent,
                CachePath = string.IsNullOrWhiteSpace(cachePath) ? null : cachePath,
            };
            return JsonConvert.SerializeObject(request, SerializerSettings);
        }

        public string GetReviewDeltaCommand(string oldScore, string newScore)
        {
            var scores = _creator.Create(oldScore, newScore);
            return $"{scores}";
        }

        private static string GetArgumentStr(params string[] args)
        {
            var sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.Append(arg).Append(" ");
            }

            return sb.ToString();
        }

        private string AdjustTelemetryQuotes(string value) => value.Replace("\"", "\\\"");

        private string SerializeRefactorRequest(FnsToRefactorRequestModel request, string fileName, string fileContent, string cachePath, PreFlightResponseModel preflight)
        {
            request.FileName = fileName;
            request.FileContent = fileContent;
            if (!string.IsNullOrWhiteSpace(cachePath))
            {
                request.CachePath = cachePath;
            }

            request.Preflight = preflight;
            return JsonConvert.SerializeObject(request, SerializerSettings);
        }
    }
}
