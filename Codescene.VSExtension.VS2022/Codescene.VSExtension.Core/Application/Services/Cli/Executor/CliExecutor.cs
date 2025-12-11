using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
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
        private readonly IProcessExecutor _executor;

        [ImportingConstructor]
        public CliExecutor(ICliCommandProvider cliCommandProvider)
        {
            _cliCommandProvider = cliCommandProvider;
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
                () => _executor.Execute(arguments, content),
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
                () => _executor.Execute("delta", arguments),
                "Delta for file failed."
            );
        }

        public PreFlightResponseModel Preflight(bool force = true)
        {
            var arguments = _cliCommandProvider.GetPreflightSupportInformationCommand(force: force);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping preflight. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<PreFlightResponseModel>(
                "ACE preflight",
                () => _executor.Execute(arguments, null, Timeout.TELEMETRY_TIMEOUT),
                "Preflight failed."
            );
        }

        public RefactorResponseModel PostRefactoring(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null)
        {
            var arguments = _cliCommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: token);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping refactoring. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<RefactorResponseModel>(
                "ACE refactoring",
                () => _executor.Execute(arguments),
                "Refactoring failed."
            );
        }

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells, string preflight)
        {
            var arguments = _cliCommandProvider.GetRefactorCommandWithCodeSmells(extension, codeSmells, preflight);

            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping refactoring functions check. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<IList<FnToRefactorModel>>(
                "ACE refactoring functions check",
                () => _executor.Execute(arguments, content),
                "Refactoring functions check failed."
            );
        }

        public string GetDeviceId()
        {
            var arguments = _cliCommandProvider.DeviceIdCommand;
            return ExecuteSimpleCommand(arguments, "Could not get device ID");
        }

        public string GetFileVersion()
        {
            var arguments = _cliCommandProvider.VersionCommand;
            return ExecuteSimpleCommand(arguments, "Could not get CLI version");
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

        private string ExecuteSimpleCommand(string arguments, string errorMessage)
        {
            try
            {
                var result = _executor.Execute(arguments);

                return result?.Trim().TrimEnd('\r', '\n');
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return "";
            }
        }
    }
}