using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        private readonly ILogger _logger;
        private readonly ICliCommandProvider _cliCommandProvider; // TODO: evaulate if this is needed, if  ExecuteCommand(string arguments, string content = null) is replaced with IProcessExecutor
        private readonly IProcessExecutor _executor;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ICacheStorageService _cacheStorageService;

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            ICliCommandProvider cliCommandProvider,
            IProcessExecutor executor,
            ISettingsProvider settingsProvider,
            ICacheStorageService cacheStorageService)
        {
            _logger = logger;
            _cliCommandProvider = cliCommandProvider;
            _executor = executor;
            _settingsProvider = settingsProvider;
            _cacheStorageService = cacheStorageService;
        }

        /// <summary>
        /// Reviews a file's content by invoking the CLI with the appropriate arguments.
        /// </summary>
        /// <param name="filename">The name of the file being reviewed (used for context, not passed to CLI).</param>
        /// <param name="content">The content (code) of the file to be reviewed.</param>
        /// <returns>A <see cref="CliReviewModel"/> containing the review results, or null if the review fails.</returns>
        public CliReviewModel ReviewContent(string filename, string content)
        {
            var command = _cliCommandProvider.ReviewFileContentCommand;
            var payload = _cliCommandProvider.GetReviewFileContentPayload(filename, content, _cacheStorageService.GetSolutionReviewCacheLocation());

            return ExecuteWithTimingAndLogging<CliReviewModel>(
                $"CLI file review",
                () => _executor.Execute(command, payload),
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
            var effectiveToken = string.IsNullOrEmpty(token) ? _settingsProvider.AuthToken : token;
            if (string.IsNullOrEmpty(effectiveToken))
            {
                throw new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
            }

            var arguments = _cliCommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: effectiveToken);
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

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            var cachePath = _cacheStorageService.GetSolutionReviewCacheLocation();
            _cacheStorageService.RemoveOldReviewCacheEntries();

            var command = _cliCommandProvider.RefactorCommand;
            var content = _cliCommandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codeSmells, preflight);

            if (string.IsNullOrEmpty(content))
            {
                _logger.Warn("Skipping refactoring functions check. Payload content was not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<IList<FnToRefactorModel>>(
                "ACE refactoring functions check",
                () => _executor.Execute(command, content),
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
            catch (DevtoolsException e)
            {
                _logger.Error(errorMessage, e);
                throw e;
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