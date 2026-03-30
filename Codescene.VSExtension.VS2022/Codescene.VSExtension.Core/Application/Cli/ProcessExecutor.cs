// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(IProcessExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class ProcessExecutor : IProcessExecutor
    {
        private readonly ICliSettingsProvider _cliSettingsProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public ProcessExecutor(ICliSettingsProvider cliSettingsProvider, ILogger logger)
        {
            _cliSettingsProvider = cliSettingsProvider ?? throw new ArgumentNullException(nameof(cliSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> ExecuteAsync(string arguments, string content = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var cliFilePath = _cliSettingsProvider.CliFileFullPath;

            if (!File.Exists(cliFilePath))
            {
                throw new FileNotFoundException(
                    $"CodeScene CLI executable not found at {cliFilePath}. " +
                    "The CLI should be bundled with the extension. " +
                    "Please reinstall the extension or contact support if this issue persists.",
                    cliFilePath);
            }

            var actualTimeout = timeout ?? Constants.Timeout.DEFAULTCLITIMEOUT;
            using var timeoutCts = new CancellationTokenSource();
            timeoutCts.CancelAfter(actualTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var processInfo = new ProcessStartInfo
            {
                FileName = cliFilePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process();
            process.StartInfo = processInfo;
            process.EnableRaisingEvents = true;

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();
            var outputTcs = new TaskCompletionSource<bool>();
            var errorTcs = new TaskCompletionSource<bool>();
            var exitTcs = new TaskCompletionSource<bool>();

            AttachOutputHandlers(new AttachOutputHandlersArgs(process, outputBuilder, errorBuilder, outputTcs, errorTcs));

            process.Exited += (_, _) => exitTcs.TrySetResult(true);

            var logCommand = TextUtils.BuildCommandForLogging(arguments, content);
            _logger.Info($"Executing CLI: {logCommand}");

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            WriteInput(process, content);

            var mainTask = Task.WhenAll(exitTcs.Task, outputTcs.Task, errorTcs.Task);
            var timeoutTask = Task.Delay(actualTimeout, linkedCts.Token);

            var completedTask = await Task.WhenAny(mainTask, timeoutTask).ConfigureAwait(false);

            if (completedTask == timeoutTask)
            {
                return CompleteOnTimeoutOrThrow(process, arguments, actualTimeout, cancellationToken);
            }

            await mainTask.ConfigureAwait(false);

            process.WaitForExit();

            return HandleResult(process, outputBuilder, errorBuilder);
        }

        private static bool ShouldPrintError(int code)
        {
            return code == 10 || code == 11;
        }

        private static DevtoolsException TryDeserializeDevtoolsException(string outputText)
        {
            if (string.IsNullOrWhiteSpace(outputText))
            {
                return null;
            }

            var trimmed = outputText.TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{')
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<DevtoolsException>(outputText);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string CompleteOnTimeoutOrThrow(
            Process process,
            string arguments,
            TimeSpan actualTimeout,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (arguments.Contains("telemetry"))
            {
                return string.Empty;
            }

            throw new TimeoutException($"Process execution exceeded the timeout of {actualTimeout.TotalMilliseconds}ms.");
        }

        private void AttachOutputHandlers(AttachOutputHandlersArgs handlerArguments)
        {
            handlerArguments.Process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    handlerArguments.OutputTcs.TrySetResult(true);
                }
                else
                {
                    handlerArguments.Output.AppendLine(e.Data);
                }
            };

            handlerArguments.Process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                {
                    handlerArguments.ErrorTcs.TrySetResult(true);
                }
                else
                {
                    handlerArguments.Error.AppendLine(e.Data);
                }
            };
        }

        private void WriteInput(Process process, string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                process.StandardInput.Write(content);
                process.StandardInput.Close();
            }
        }

        private string HandleResult(Process process, StringBuilder output, StringBuilder error)
        {
            var code = process.ExitCode;
            var outputText = output.ToString();
            var errorText = error.ToString();

            if (code != 0)
            {
                var devtoolsEx = TryDeserializeDevtoolsException(outputText);
                if (devtoolsEx != null)
                {
                    throw devtoolsEx;
                }

                if (ShouldPrintError(code))
                {
                    throw new Exception(
                        $"Process exited with code {code}. Could not parse CLI error output. Output: {outputText}. Error stream: {errorText}");
                }

                var detail = string.IsNullOrWhiteSpace(errorText) ? outputText : errorText;
                if (string.IsNullOrWhiteSpace(detail))
                {
                    detail = "(no output)";
                }

                throw new Exception($"Process exited with code {code}. Error: {detail}");
            }

            return outputText;
        }
    }
}
