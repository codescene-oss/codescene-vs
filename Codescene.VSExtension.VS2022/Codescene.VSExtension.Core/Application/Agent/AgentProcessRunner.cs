// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal class AgentProcessRunner
    {
        private static readonly Regex AnsiEscapeRegex = new Regex(
            @"\x1B\[[0-9;]*[a-zA-Z]",
            RegexOptions.Compiled);

        private readonly IAgentProcessFactory _factory;
        private readonly ILogger _logger;
        private readonly IAgentProcessTracker _tracker;

        public AgentProcessRunner(IAgentProcessFactory factory, ILogger logger, IAgentProcessTracker tracker = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracker = tracker;
        }

        public async Task<AgentProcessResult> RunAsync(
            AgentProcessStartInfo startInfo,
            Action<string> onProgress,
            TimeSpan timeout,
            IEnumerable<string> secrets,
            CancellationToken cancellationToken)
        {
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }

            var redactor = new AgentRedactor(secrets);
            var stderr = new StringBuilder();

            _logger.Debug("Starting cs-agent with arguments: " + redactor.Redact(startInfo.Arguments));

            IAgentProcess process = null;

            try
            {
                process = _factory.Start(startInfo);
                SubscribeToOutput(process, redactor, onProgress, stderr);
                _tracker?.Track(process);

                var exitTask = process.WaitForExitAsync();
                var timeoutTask = Task.Delay(timeout, cancellationToken);

                var completedTask = await Task.WhenAny(exitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        process.KillTree();
                        throw new AgentFailure(AgentFailureKind.Cancelled, "Agent process was cancelled");
                    }
                    else
                    {
                        var timeoutMinutes = (int)timeout.TotalMinutes;
                        var message = $"The Refactoring Agent did not finish within {timeoutMinutes} minutes.";
                        _logger.Warn(message);
                        process.KillTree();
                        throw new AgentFailure(AgentFailureKind.Generic, message);
                    }
                }

                var exitCode = await exitTask;

                return new AgentProcessResult
                {
                    ExitCode = exitCode,
                    Stderr = stderr.ToString(),
                };
            }
            catch (AgentFailure)
            {
                throw;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                process?.KillTree();
                throw new AgentFailure(AgentFailureKind.Cancelled, "Agent process was cancelled");
            }
            finally
            {
                process?.Dispose();
            }
        }

        private void SubscribeToOutput(IAgentProcess process, AgentRedactor redactor, Action<string> onProgress, StringBuilder stderr)
        {
            process.StdoutLine += (_, line) =>
            {
                var redacted = redactor.Redact(line);
                _logger.Debug("cs-agent stdout: " + redacted);

                if (!string.IsNullOrWhiteSpace(line))
                {
                    onProgress?.Invoke(redacted);
                }
            };

            process.StderrLine += (_, line) =>
            {
                var stripped = AnsiEscapeRegex.Replace(line, string.Empty);
                var redacted = redactor.Redact(stripped);
                _logger.Debug("cs-agent stderr: " + redacted);
                stderr.AppendLine(stripped);
            };
        }
    }
}
