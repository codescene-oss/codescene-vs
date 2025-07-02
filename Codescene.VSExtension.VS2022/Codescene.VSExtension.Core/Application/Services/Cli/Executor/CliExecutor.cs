using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;
namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        private const int DEFAULT_TIMEOUT_MS = 10000;
        private static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT_MS);

        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

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
                () => _executor.Execute(arguments, content, DEFAULT_TIMEOUT),
                $"Review of file {filename} failed"
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

        public string GetDeviceId()
        {
            try
            {
                var arguments = "telemetry --device-id";
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