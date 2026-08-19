// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    [Export(typeof(IIdeServerHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class IdeServerHost : IIdeServerHost
    {
        private readonly ICliSettingsProvider _settings;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private IdeServerProcess _process;
        private bool _restarting;
        private bool _disposed;

        [ImportingConstructor]
        public IdeServerHost(ICliSettingsProvider settings, ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler Restarted;

        public IIdeServerClient Client => _process?.Client;

        public ServerStartMetadata Metadata { get; private set; }

        public bool IsRunning => Client != null && Client.IsConnected;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                if (IsRunning)
                {
                    return;
                }

                await StartProcessAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();
                DisposeProcess();
                await StartProcessAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            Restarted?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeProcess();
            _gate.Dispose();
        }

        private async Task StartProcessAsync(CancellationToken cancellationToken)
        {
            var process = new IdeServerProcess(_settings, _logger);
            process.Exited += OnProcessExited;
            var metadata = await process.StartAsync(cancellationToken).ConfigureAwait(false);
            if (!string.Equals(metadata?.Sha, _settings.RequiredDevToolVersion, StringComparison.Ordinal))
            {
                process.Exited -= OnProcessExited;
                process.Dispose();
                throw new InvalidOperationException(
                    $"IDE server SHA '{metadata?.Sha}' does not match required version '{_settings.RequiredDevToolVersion}'.");
            }

            _process = process;
            Metadata = metadata;
            _logger.Info($"CodeScene IDE server started ({metadata.Version}, {metadata.Sha}).");
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            if (_disposed || _restarting)
            {
                return;
            }

            _logger.Warn("CodeScene IDE server process exited unexpectedly. Restarting.");
            _ = RestartAfterCrashAsync();
        }

        private async Task RestartAfterCrashAsync()
        {
            _restarting = true;
            try
            {
                await RestartAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to restart the CodeScene IDE server.", ex);
            }
            finally
            {
                _restarting = false;
            }
        }

        private void DisposeProcess()
        {
            if (_process == null)
            {
                return;
            }

            _process.Exited -= OnProcessExited;
            _process.Dispose();
            _process = null;
            Metadata = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IdeServerHost));
            }
        }
    }
}
