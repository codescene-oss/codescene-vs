using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICliCommandProvider _cliCommandProvider; // TODO: evaulate if this is needed, if  ExecuteCommand(string arguments, string content = null) is replaced with IProcessExecutor

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [Import]
        private readonly IProcessExecutor _executor;

        [ImportingConstructor]
        public CliExecutor(ICliCommandProvider cliCommandProvider, ICliSettingsProvider cliSettingsProvider)
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

        /// <summary>
        /// Reviews a file's content by invoking the CLI with the appropriate arguments.
        /// </summary>
        /// <param name="filename">The name of the file being reviewed (used for context, not passed to CLI).</param>
        /// <param name="content">The content (code) of the file to be reviewed.</param>
        /// <returns>A <see cref="CliReviewModel"/> containing the review results, or null if the review fails.</returns>
        public CliReviewModel ReviewContent(string filename, string content)
        {
            var arguments = _cliCommandProvider.GetReviewFileContentCommand(filename);

            return ExecuteWithTimingAndLogging<CliReviewModel>(
                "CLI file review",
                () => _executor.Execute(arguments, content, Timeout.DEFAULT_CLI_TIMEOUT),
                $"Review of file {filename} failed"
            );
        }

        /// <summary>
        /// Executes a delta review between two versions of a file using their respective scores.
        /// Either <paramref name="oldScore"/> or <paramref name="newScore"/> may be null, but not both.
        /// </summary>
        /// <param name="oldScore">The raw score of the old file version's review.</param>
        /// <param name="newScore">The raw score of the new file version's review.</param>
        /// <returns>A <see cref="DeltaResponseModel"/> containing delta results, or null if execution fails or arguments are invalid.</returns>
        public DeltaResponseModel ReviewDelta(string oldScore, string newScore)
        {
            var arguments = _cliCommandProvider.GetReviewDeltaCommand(oldScore, newScore);

            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<DeltaResponseModel>(
                "CLI file delta review",
                () => _executor.Execute("delta", arguments, Timeout.DEFAULT_CLI_TIMEOUT),
                "Delta for file failed."
            );
        }

        private T ExecuteWithTimingAndLogging<T>(string label, Func<string> execute, string errorMessage)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = execute();
                stopwatch.Stop();

                _logger.Debug($"{Titles.CODESCENE} {label} completed in {stopwatch.ElapsedMilliseconds} ms.");
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return default;
            }
        }

        // TODO: possibly replace this implementation completely with IProcessExecutor
        private string ExecuteCommand(string arguments, string content = null)
        {
            var exePath = _cliSettingsProvider.CliFileFullPath;
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable file {exePath} can not be found on the location!");
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                var outputTcs = new TaskCompletionSource<bool>();
                var errorTcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        outputTcs.TrySetResult(true);
                    else
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        errorTcs.TrySetResult(true);
                    else
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    process.StandardInput.Write(content);
                    process.StandardInput.Close();
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process exit and output read
                var waitTask = Task.Run(() =>
                {
                    process.WaitForExit();
                    outputTcs.Task.Wait();
                    errorTcs.Task.Wait();
                });

                if (!waitTask.Wait(10000))
                {
                    try
                    {
                        process.Kill(); // true = entire process tree
                    }
                    catch (Exception ex)
                    {
                        throw new TimeoutException("Process timed out and could not be killed.", ex);
                    }

                    throw new TimeoutException("Process execution exceeded the timeout of 10 seconds.");
                }

                if (errorBuilder.Length > 0)
                {
                    throw new InvalidOperationException($"Process error output: {errorBuilder}");
                }

                return outputBuilder.ToString();
            }
        }


        // TODO: possibly replace this implementation completely with IProcessExecutor
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

            if (!string.IsNullOrWhiteSpace(content) || arguments.Contains("delta"))
            {
                proc.StandardInput.Write(content);
                proc.StandardInput.Close();
            }

            return tcs.Task;
        }

        public string GetFileVersion()
        {
            var arguments = _cliCommandProvider.VersionCommand;
            var result = ExecuteCommand(arguments); // TODO: possibly replace this implementation completely with IProcessExecutor
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

        public string GetDeviceId()
        {
            try
            {
                var arguments = _cliCommandProvider.DeviceIdCommand;
                var result = _executor.Execute(arguments);

                return result?.Trim();
            }
            catch (Exception e)
            {
                _logger.Error($"Could not get device ID", e);
                return "";
            }
        }
    }
}