// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    [Export(typeof(IAgentConnectivityTester))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AgentConnectivityTester : IAgentConnectivityTester
    {
        private readonly IAgentSkillRunner _skillRunner;
        private readonly IAuthService _authService;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public AgentConnectivityTester(
            IAgentSkillRunner skillRunner,
            IAuthService authService,
            ILogger logger)
        {
            _skillRunner = skillRunner ?? throw new ArgumentNullException(nameof(skillRunner));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

        public async Task<ConnectivityResult> TestAsync(string workDir, CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(Timeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var snapshot = _authService?.Current ?? AuthSnapshot.Inactive;
                var runner = _skillRunner as AgentSkillRunner;

                if (runner != null)
                {
                    await runner.RunAsync<object>(
                        AgentSkill.CheckConnectivity,
                        workDir,
                        null,
                        null,
                        snapshot,
                        linkedCts.Token);
                }
                else
                {
                    await _skillRunner.RunAsync<object>(
                        AgentSkill.CheckConnectivity,
                        workDir,
                        null,
                        null,
                        linkedCts.Token);
                }

                return new ConnectivityResult
                {
                    Success = true,
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new ConnectivityResult
                {
                    Cancelled = true,
                };
            }
            catch (OperationCanceledException)
            {
                return new ConnectivityResult
                {
                    Error = "Connectivity test timed out",
                };
            }
            catch (AgentFailure ex)
            {
                return new ConnectivityResult
                {
                    Error = ex.Message,
                };
            }
            catch (Exception ex)
            {
                return new ConnectivityResult
                {
                    Error = $"Process exited with code {ExtractExitCode(ex)}",
                };
            }
        }

        private int ExtractExitCode(Exception ex)
        {
            if (ex.Message.Contains("exited with code"))
            {
                var parts = ex.Message.Split(new[] { "exited with code" }, StringSplitOptions.None);
                if (parts.Length > 1)
                {
                    var codePart = parts[1].Trim().Split(' ')[0].Trim('.');
                    if (int.TryParse(codePart, out var code))
                    {
                        return code;
                    }
                }
            }

            return -1;
        }
    }
}
