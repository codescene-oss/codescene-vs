// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Application.Agent
{
    [Export(typeof(IAgentSkillRunner))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AgentSkillRunner : IAgentSkillRunner
    {
        private static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(20);

        private readonly ISettingsProvider _settings;
        private readonly ICliSettingsProvider _cliSettings;
        private readonly IProviderKeyStore _keyStore;
        private readonly IIdeServerAuthClient _authClient;
        private readonly IAuthService _authService;
        private readonly IAgentProcessFactory _processFactory;
        private readonly IAgentProcessTracker _processTracker;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public AgentSkillRunner(
            ISettingsProvider settings,
            ICliSettingsProvider cliSettings,
            IProviderKeyStore keyStore,
            IIdeServerAuthClient authClient,
            IAuthService authService,
            IAgentProcessFactory processFactory,
            IAgentProcessTracker processTracker,
            ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _cliSettings = cliSettings ?? throw new ArgumentNullException(nameof(cliSettings));
            _keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            _authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
            _authService = authService;
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _processTracker = processTracker ?? throw new ArgumentNullException(nameof(processTracker));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TOutput> RunAsync<TOutput>(
            AgentSkill skill,
            string workDir,
            object input,
            Action<string> onProgress,
            CancellationToken cancellationToken)
            where TOutput : class
        {
            return await RunAsync<TOutput>(skill, workDir, input, onProgress, _authService?.Current, cancellationToken);
        }

        internal async Task<TOutput> RunAsync<TOutput>(
            AgentSkill skill,
            string workDir,
            object input,
            Action<string> onProgress,
            AuthSnapshot snapshot,
            CancellationToken cancellationToken)
            where TOutput : class
        {
            if (skill == null)
            {
                throw new ArgumentNullException(nameof(skill));
            }

            string ioDir = null;

            try
            {
                ioDir = PrepareIoDirectory(skill, input);
                var token = await ResolveTokenAsync(snapshot, cancellationToken);

                AgentBinary.Verify(_cliSettings, _logger);

                var keys = _keyStore.GetAll();
                LogConfiguredKeys(keys);

                var startInfo = BuildStartInfo(skill, workDir, ioDir, snapshot, token, keys);
                var secrets = new List<string> { token };
                secrets.AddRange(keys.Values);

                var runner = new AgentProcessRunner(_processFactory, _logger, _processTracker);
                var result = await runner.RunAsync(startInfo, onProgress, RunTimeout, secrets, cancellationToken);

                var redactor = new AgentRedactor(secrets);
                EnsureSucceeded(result, redactor);
                return ReadOutput<TOutput>(skill, ioDir, result, redactor);
            }
            finally
            {
                DeleteIoDirectory(ioDir);
            }
        }

        private static string PrepareIoDirectory(AgentSkill skill, object input)
        {
            if (skill.InputFile == null)
            {
                return null;
            }

            var ioDir = Path.Combine(Path.GetTempPath(), "cs-agent-io-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ioDir);

            var inputPath = Path.Combine(ioDir, skill.InputFile);
            var inputJson = JsonConvert.SerializeObject(input, Formatting.None);
            File.WriteAllText(inputPath, inputJson, Encoding.UTF8);
            return ioDir;
        }

        private static string LastStderrLines(string stderr, AgentRedactor redactor)
        {
            var stderrLines = (stderr ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => redactor.Redact(line))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return stderrLines.Count > 5
                ? string.Join(" ", stderrLines.Skip(stderrLines.Count - 5))
                : string.Join(" ", stderrLines);
        }

        private async Task<string> ResolveTokenAsync(AuthSnapshot snapshot, CancellationToken cancellationToken)
        {
            var tokenResolver = new AgentTokenResolver(_settings, snapshot ?? AuthSnapshot.Inactive, _authClient, _logger);
            var token = await tokenResolver.ResolveAsync(cancellationToken);

            if (string.IsNullOrEmpty(token))
            {
                throw Fail(AgentFailureKind.Auth, "Refactoring requires you to be signed in to CodeScene. Sign in, or set a personal access token in the \"Auth Token\" setting.");
            }

            return token;
        }

        private void LogConfiguredKeys(IDictionary<string, string> keys)
        {
            var hasAnthropicKey = keys.ContainsKey("anthropic");
            var hasOpenAiKey = keys.ContainsKey("openai");
            var hasGoogleKey = keys.ContainsKey("google");

            _logger.Debug($"Provider keys configured: anthropic={hasAnthropicKey}, openai={hasOpenAiKey}, google={hasGoogleKey}");
        }

        private AgentProcessStartInfo BuildStartInfo(
            AgentSkill skill,
            string workDir,
            string ioDir,
            AuthSnapshot snapshot,
            string token,
            IDictionary<string, string> keys)
        {
            var model = AgentModelResolver.Resolve(_settings, keys.Keys.ToList());
            var config = AgentConfigBuilder.Build(token, skill, ioDir, snapshot, (IReadOnlyDictionary<string, string>)keys, _settings);

            return new AgentProcessStartInfo
            {
                FileName = _cliSettings.AgentFileFullPath,
                Arguments = $"run skill:{skill.Name} --model {model}",
                WorkingDirectory = workDir ?? Path.GetTempPath(),
                EnvironmentAdditions = new Dictionary<string, string>
                {
                    ["CS_AGENT_CONFIG"] = config,
                },
            };
        }

        private void EnsureSucceeded(AgentProcessResult result, AgentRedactor redactor)
        {
            if (result.ExitCode != 0)
            {
                var redactedStderr = redactor.Redact(result.Stderr ?? string.Empty);
                throw Fail(AgentFailureKind.Generic, $"The Refactoring Agent exited with code {result.ExitCode}. {redactedStderr.Trim()}");
            }
        }

        private TOutput ReadOutput<TOutput>(AgentSkill skill, string ioDir, AgentProcessResult result, AgentRedactor redactor)
            where TOutput : class
        {
            if (skill.OutputFile == null)
            {
                return null;
            }

            var outputPath = Path.Combine(ioDir, skill.OutputFile);

            if (!File.Exists(outputPath))
            {
                throw Fail(AgentFailureKind.Generic, $"The Refactoring Agent did not produce the expected output file. Last stderr lines: {LastStderrLines(result.Stderr, redactor)}");
            }

            var outputJson = File.ReadAllText(outputPath, Encoding.UTF8);

            try
            {
                return JsonConvert.DeserializeObject<TOutput>(outputJson);
            }
            catch (JsonException ex)
            {
                throw Fail(AgentFailureKind.Generic, $"Failed to parse agent output: {ex.Message}");
            }
        }

        private AgentFailure Fail(AgentFailureKind kind, string message)
        {
            _logger.Warn(message);
            return new AgentFailure(kind, message);
        }

        private void DeleteIoDirectory(string ioDir)
        {
            if (ioDir == null || !Directory.Exists(ioDir))
            {
                return;
            }

            try
            {
                Directory.Delete(ioDir, true);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to delete agent I/O directory: {ex.Message}");
            }
        }
    }
}
