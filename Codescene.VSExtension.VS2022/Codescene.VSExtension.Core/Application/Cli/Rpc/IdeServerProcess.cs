// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public sealed class IdeServerProcess : IDisposable
    {
        public static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);

        private readonly ICliSettingsProvider _settings;
        private readonly ILogger _logger;
        private Process _process;
        private IdeServerClient _client;
        private TaskCompletionSource<ServerStartMetadata> _started;
        private bool _disposed;

        public IdeServerProcess(ICliSettingsProvider settings, ILogger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler Exited;

        public IIdeServerClient Client => _client;

        public ServerStartMetadata Metadata { get; private set; }

        public int? ProcessId => _process?.HasExited == false ? _process.Id : (int?)null;

        public async Task<ServerStartMetadata> StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureDistributionExists();

            _started = new TaskCompletionSource<ServerStartMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startInfo = CreateStartInfo();
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };
            process.ErrorDataReceived += OnErrorDataReceived;
            process.Exited += OnProcessExited;

            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("Failed to start the CodeScene IDE server process.");
            }

            _process = process;
            process.BeginErrorReadLine();

            _client = IdeServerClient.Attach(OpenStandardInput(process), process.StandardOutput.BaseStream, _logger);
            _client.Started += OnClientStarted;
            _client.Disconnected += OnClientDisconnected;

            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeoutCts.CancelAfter(StartupTimeout);
                using (timeoutCts.Token.Register(() => _started.TrySetCanceled(timeoutCts.Token)))
                {
                    try
                    {
                        Metadata = await _started.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        DisposeProcess();
                        throw new TimeoutException("Timed out waiting for cs-ide/start from the CodeScene IDE server.");
                    }
                    catch
                    {
                        DisposeProcess();
                        throw;
                    }
                }
            }

            return Metadata;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeProcess();
        }

        private static Stream OpenStandardInput(Process process)
        {
            var previousEncoding = Console.InputEncoding;
            Console.InputEncoding = new UTF8Encoding(false);
            try
            {
                process.StandardInput.AutoFlush = true;
                return process.StandardInput.BaseStream;
            }
            finally
            {
                Console.InputEncoding = previousEncoding;
            }
        }

        private ProcessStartInfo CreateStartInfo()
        {
            var threads = Math.Max(1, Environment.ProcessorCount / 2);
            return new ProcessStartInfo
            {
                FileName = _settings.JavaExeFullPath,
                Arguments = $"--enable-native-access=ALL-UNNAMED -jar \"{_settings.JarFullPath}\" server --threads {threads}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                WorkingDirectory = _settings.DistributionFullPath,
            };
        }

        private void EnsureDistributionExists()
        {
            if (!File.Exists(_settings.JavaExeFullPath) || !File.Exists(_settings.JarFullPath))
            {
                throw new FileNotFoundException(
                    "CodeScene IDE server distribution is incomplete. Expected jre\\bin\\java.exe and cs-ide.jar under " +
                    _settings.DistributionFullPath);
            }
        }

        private void OnClientStarted(object sender, ServerStartMetadata metadata)
        {
            _started?.TrySetResult(metadata);
        }

        private void OnClientDisconnected(object sender, EventArgs e)
        {
            _started?.TrySetException(new InvalidOperationException("IDE server disconnected before cs-ide/start."));
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            _started?.TrySetException(new InvalidOperationException("IDE server process exited before cs-ide/start."));
            Exited?.Invoke(this, EventArgs.Empty);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger.Debug($"cs-ide: {e.Data}");
            }
        }

        private void DisposeProcess()
        {
            if (_client != null)
            {
                _client.Started -= OnClientStarted;
                _client.Disconnected -= OnClientDisconnected;
                _client.Dispose();
                _client = null;
            }

            var process = _process;
            _process = null;
            if (process == null)
            {
                return;
            }

            process.ErrorDataReceived -= OnErrorDataReceived;
            process.Exited -= OnProcessExited;
            KillProcessTree(process);
            process.Dispose();
        }

        private void KillProcessTree(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            try
            {
                using (var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/pid {process.Id} /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }))
                {
                    killer?.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to kill IDE server process tree: {ex.Message}");
                try
                {
                    process.Kill();
                }
                catch
                {
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IdeServerProcess));
            }
        }
    }
}
