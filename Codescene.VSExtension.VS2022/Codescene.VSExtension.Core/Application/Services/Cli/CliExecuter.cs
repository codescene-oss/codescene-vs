using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecuter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecuter : ICliExecuter
    {
        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;

        public CliExecuter(ICliCommandProvider cliCommandProvider, ICliSettingsProvider cliSettingsProvider)
        {
            _cliCommandProvider = cliCommandProvider;
            _cliSettingsProvider = cliSettingsProvider;
        }

        public CliReviewModel Review(string path)
        {
            var arguments = _cliCommandProvider.GetReviewPathCommand(path);
            var result = ExecuteCommand(arguments);
            return JsonConvert.DeserializeObject<CliReviewModel>(result);
        }

        public CliReviewModel ReviewContent(string filename, string content)
        {
            var arguments = _cliCommandProvider.GetReviewFileContentCommand(filename);
            var result = ExecuteCommand(arguments, content: content);
            return JsonConvert.DeserializeObject<CliReviewModel>(result);
        }

        private string ExecuteCommand(string arguments, string content = null)
        {
            var exePath = _cliSettingsProvider.CliFileFullPath;
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable file {exePath} can not be found on the location!");
            }

            var processInfo = new ProcessStartInfo()
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process.StandardInput != null && string.IsNullOrWhiteSpace(content) == false)
                {
                    process.StandardInput.Write(content);
                    process.StandardInput.Close(); // Close input stream to signal end of input
                }
                string result = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return result;
            }
        }

        public string GetFileVersion()
        {
            var arguments = _cliCommandProvider.VersionCommand;
            var result = ExecuteCommand(arguments);
            return result.TrimEnd('\r', '\n');
        }

        public PreFlightResponseModel Preflight(bool force = true)
        {
            var arguments = _cliCommandProvider.GetPreflightSupportInformationCommand(force: force);
            var result = ExecuteCommand(arguments);
            return JsonConvert.DeserializeObject<PreFlightResponseModel>(result);
        }

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmellsJson)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmellsJson);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmellsJson, string preflight)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmellsJson, preflight);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public RefactorResponseModel PostRefactoring(string content, string fnToRefactor, bool skipCache = false, string token = null)
        {
            var arguments = _cliCommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: token);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<RefactorResponseModel>(result);
        }

        public IList<FnToRefactorModel> FnsToRefactorFromDelta(string content, string extension, string delta)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithDeltaResult(extension: extension, deltaResult: delta);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public IList<FnToRefactorModel> FnsToRefactorFromDelta(string content, string extension, string delta, string preflight)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithDeltaResult(extension: extension, deltaResult: delta, preflight: preflight);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public DeltaResponseModel ReviewDelta(string content, string oldScore, string newScore)
        {
            //var arguments = _cliCommandProvider.get(extension: extension, deltaResult: delta, preflight: preflight);
            //var result = ExecuteCommand(arguments, content);
            //return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
            return null;
        }
    }
}