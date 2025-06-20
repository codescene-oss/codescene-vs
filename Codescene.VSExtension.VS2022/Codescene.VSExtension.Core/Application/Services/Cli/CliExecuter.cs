using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecuter))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecuter : ICliExecuter
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [ImportingConstructor]
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

            var stopwatch = Stopwatch.StartNew();
            var result = ExecuteCommand(arguments, content: content);
            stopwatch.Stop();

            _logger.Debug($"{Titles.CODESCENE} CLI file review completed in {stopwatch.ElapsedMilliseconds} ms.");
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

        private Task<(string StdOut, string StdErr, int ExitCode)> ExecuteCommandAsync(string arguments, string content = null)
        {
            var exePath = _cliSettingsProvider.CliFileFullPath;
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable file {exePath} can not be found on the location!");
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var tcs = new TaskCompletionSource<(string, string, int)>();

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Exited += (_, __) =>
            {
                tcs.TrySetResult((stdout.ToString(), stderr.ToString(), proc.ExitCode));
                proc.Dispose();
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            if (!string.IsNullOrWhiteSpace(content))
            {
                proc.StandardInput.Write(content);
                proc.StandardInput.Close();
            }

            return tcs.Task;
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

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmells);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells, string preflight)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmells, preflight);
            var result = ExecuteCommand(arguments, content);
            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result);
        }

        public async Task<RefactorResponseModel> PostRefactoring(string fnToRefactor, bool skipCache = false, string token = null)
        {
            var arguments = _cliCommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: token);
            var result = await ExecuteCommandAsync(arguments);
            return JsonConvert.DeserializeObject<RefactorResponseModel>(result.StdOut);
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

        public async Task<DeltaResponseModel> ReviewDelta(string content, string oldScore, string newScore)
        {
            var arguments = _cliCommandProvider.GetReviewDeltaCommand(oldScore: oldScore, newScore: newScore);
            var result = await ExecuteCommandAsync(arguments, content);
            return JsonConvert.DeserializeObject<DeltaResponseModel>(result.StdOut);
        }

        public Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string content, string extension, string codeSmells)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmells);
            return FnsToRefactorFromCodeSmellsAsync(arguments, content);
        }

        public Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string content, string extension, string codeSmells, string preflight)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmells, preflight);
            return FnsToRefactorFromCodeSmellsAsync(arguments, content);
        }

        private async Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string arguments, string content)
        {
            var result = await ExecuteCommandAsync(arguments, content);

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                throw new System.Exception($"FnsToRefactorFromCodeSmellsAsync, error:{result.StdErr}");
            }

            return JsonConvert.DeserializeObject<List<FnToRefactorModel>>(result.StdOut);
        }
    }
}