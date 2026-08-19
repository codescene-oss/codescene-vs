// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    [Export(typeof(IWorkspaceReviewCoordinator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204", Justification = "Private static helpers follow the instance methods that use them.")]
    public sealed class WorkspaceReviewCoordinator : IWorkspaceReviewCoordinator, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IIdeServerHost _host;
        private readonly IWorkspaceReviewListener _listener;
        private readonly IGitService _gitService;
        private readonly ISupportedFileChecker _supportedFileChecker;
        private readonly IOpenDocumentContentProvider _openDocumentContentProvider;
        private readonly IOpenFilesObserver _openFilesObserver;
        private readonly ISavedFilesTracker _savedFilesTracker;
        private readonly HashSet<string> _watchedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();
        private bool _started;
        private bool _disposed;

        [ImportingConstructor]
        public WorkspaceReviewCoordinator(
            ILogger logger,
            IIdeServerHost host,
            IWorkspaceReviewListener listener,
            IGitService gitService,
            ISupportedFileChecker supportedFileChecker,
            [Import(AllowDefault = true)] IOpenDocumentContentProvider openDocumentContentProvider = null,
            [Import(AllowDefault = true)] IOpenFilesObserver openFilesObserver = null,
            [Import(AllowDefault = true)] ISavedFilesTracker savedFilesTracker = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _listener = listener ?? throw new ArgumentNullException(nameof(listener));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _supportedFileChecker = supportedFileChecker ?? throw new ArgumentNullException(nameof(supportedFileChecker));
            _openDocumentContentProvider = openDocumentContentProvider;
            _openFilesObserver = openFilesObserver;
            _savedFilesTracker = savedFilesTracker;
            _host.Restarted += OnHostRestarted;
        }

        public async Task StartAsync(string solutionPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_host.Client == null)
            {
                _logger.Warn("IDE server is not running. Skipping workspace watch.");
                return;
            }

            Stop();
            _listener.Attach(_host.Client);
            var roots = DiscoverGitRoots(solutionPath, workspacePaths);
            lock (_gate)
            {
                _started = true;
            }

            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await StartWatchForRootAsync(root, workspacePaths, cancellationToken);
            }
        }

        public void Stop()
        {
            List<string> roots;
            lock (_gate)
            {
                _started = false;
                roots = _watchedRoots.ToList();
                _watchedRoots.Clear();
            }

            var client = _host.Client;
            if (client != null)
            {
                foreach (var root in roots)
                {
                    client.StopWatchFiles(new StopWatchFilesParams { RepoRoot = root });
                }
            }

            _listener.Detach();
        }

        public void SubmitBufferReview(string absolutePath, string content)
        {
            if (string.IsNullOrEmpty(absolutePath) || content == null)
            {
                return;
            }

            var repoRoot = RepoPathUtil.DiscoverGitRoot(absolutePath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                _logger.Debug($"Skipping buffer review for '{absolutePath}'. No git root.");
                return;
            }

            if (_host.Client == null)
            {
                return;
            }

            EnsureAttached();
            var relPath = RepoPathUtil.ToRelPath(repoRoot, absolutePath);
            var baseline = _gitService.GetBaselineCommit(repoRoot);
            _listener.SubmitBufferReview(repoRoot, relPath, absolutePath, content, baseline);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _host.Restarted -= OnHostRestarted;
            Stop();
        }

        private async Task StartWatchForRootAsync(string repoRoot, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken)
        {
            var client = _host.Client;
            if (client == null)
            {
                return;
            }

            var baseline = _gitService.GetBaselineCommit(repoRoot);
            lock (_gate)
            {
                _watchedRoots.Add(repoRoot);
            }

            client.WatchFiles(new WatchFilesParams { RepoRoot = repoRoot, BaselineRevision = baseline });

            var detector = new GitChangeDetector(_logger, _supportedFileChecker, _gitService);
            var changed = await detector.GetChangedFilesVsBaselineAsync(
                repoRoot,
                workspacePaths,
                _savedFilesTracker,
                _openFilesObserver,
                baseline,
                cancellationToken);

            var submittedBuffers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_openFilesObserver != null && _openDocumentContentProvider != null)
            {
                foreach (var openPath in _openFilesObserver.GetAllVisibleFileNames() ?? Array.Empty<string>())
                {
                    if (!_supportedFileChecker.IsSupported(openPath) || _gitService.IsFileIgnored(openPath))
                    {
                        continue;
                    }

                    var root = RepoPathUtil.DiscoverGitRoot(openPath);
                    if (!string.Equals(root, repoRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var content = await _openDocumentContentProvider.GetContentForReviewAsync(openPath, cancellationToken);
                    if (content == null)
                    {
                        continue;
                    }

                    var relPath = RepoPathUtil.ToRelPath(repoRoot, openPath);
                    _listener.SubmitBufferReview(repoRoot, relPath, openPath, content, baseline);
                    submittedBuffers.Add(Path.GetFullPath(openPath));
                }
            }

            var diskRelPaths = new List<string>();
            foreach (var file in changed ?? new List<string>())
            {
                if (!_supportedFileChecker.IsSupported(file) || _gitService.IsFileIgnored(file))
                {
                    continue;
                }

                var full = Path.GetFullPath(file);
                if (submittedBuffers.Contains(full))
                {
                    continue;
                }

                diskRelPaths.Add(RepoPathUtil.ToRelPath(repoRoot, full));
            }

            _listener.SubmitDiskReviews(repoRoot, diskRelPaths, baseline);
        }

        private void OnHostRestarted(object sender, EventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            bool shouldRestart;
            lock (_gate)
            {
                shouldRestart = _started;
            }

            if (!shouldRestart || _host.Client == null)
            {
                return;
            }

            _listener.Attach(_host.Client);
            var roots = _watchedRoots.ToList();
            foreach (var root in roots)
            {
                var baseline = _gitService.GetBaselineCommit(root);
                _host.Client.WatchFiles(new WatchFilesParams { RepoRoot = root, BaselineRevision = baseline });
            }
        }

        private void EnsureAttached()
        {
            if (_host.Client != null)
            {
                _listener.Attach(_host.Client);
            }
        }

        private static List<string> DiscoverGitRoots(string solutionPath, IReadOnlyCollection<string> workspacePaths)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in (workspacePaths ?? Array.Empty<string>()).Concat(new[] { solutionPath }))
            {
                var root = RepoPathUtil.DiscoverGitRoot(path);
                if (!string.IsNullOrEmpty(root))
                {
                    roots.Add(root);
                }
            }

            return roots.ToList();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(WorkspaceReviewCoordinator));
            }
        }
    }
}
