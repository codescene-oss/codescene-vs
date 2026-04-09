// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Codescene.VSExtension.VS2022.Cache
{
    [Export(typeof(ICacheStorageService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CacheStorageService : ICacheStorageService
    {
        private const string REVIEWRESULTSFOLDER = ".review-results";

        [Import]
        private IAsyncTaskScheduler _scheduler;

        private string _cachePath;
        private string _workspaceDirectory;
        private bool _initialized;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            await UpdateCachePathAsync();

            VS.Events.SolutionEvents.OnAfterOpenSolution += _ => _scheduler.Schedule(ct => UpdateCachePathAsync());
            VS.Events.SolutionEvents.OnAfterCloseSolution += () => _scheduler.Schedule(ct => UpdateCachePathAsync());
            VS.Events.SolutionEvents.OnAfterOpenFolder += _ => _scheduler.Schedule(ct => UpdateCachePathAsync());
            VS.Events.SolutionEvents.OnAfterCloseFolder += _ => _scheduler.Schedule(ct => UpdateCachePathAsync());
        }

        public string GetSolutionReviewCacheLocation()
        {
            if (_cachePath == null)
            {
                return string.Empty;
            }

            var baseLocation = _cachePath;
            var location = Path.Combine(baseLocation, REVIEWRESULTSFOLDER);
            if (!Directory.Exists(location))
            {
                Directory.CreateDirectory(location);
            }

            return location;
        }

        public string GetWorkspaceDirectory()
        {
            return string.IsNullOrEmpty(_workspaceDirectory) ? string.Empty : _workspaceDirectory;
        }

        public void RemoveOldReviewCacheEntries(int nrOfDays = 30)
        {
            if (_cachePath == null)
            {
                return;
            }

            var baseLocation = _cachePath;
            var location = Path.Combine(baseLocation, REVIEWRESULTSFOLDER);
            var files = Directory.GetFiles(location);
            foreach (var fileName in files)
            {
                if (File.GetLastWriteTimeUtc(fileName) <= DateTime.UtcNow.AddDays(-nrOfDays))
                {
                    File.Delete(fileName);
                }
            }
        }

        private static string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 16);
        }

        private async Task UpdateCachePathAsync()
        {
            var workspaceId = await GetWorkspaceIdentifierAsync();

            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Codescene");

            if (!string.IsNullOrEmpty(workspaceId))
            {
                var hash = ComputeHash(workspaceId);
                _cachePath = Path.Combine(basePath, "WorkspaceCache", hash);
                _workspaceDirectory = Directory.Exists(workspaceId)
                    ? Path.GetFullPath(workspaceId.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    : Path.GetFullPath(Path.GetDirectoryName(workspaceId) ?? string.Empty);
                if (string.IsNullOrEmpty(_workspaceDirectory))
                {
                    _workspaceDirectory = null;
                }
            }
            else
            {
                _cachePath = null;
                _workspaceDirectory = null;
            }

            if (_cachePath != null)
            {
                Directory.CreateDirectory(_cachePath);
            }
        }

        private async Task<string> GetWorkspaceIdentifierAsync()
        {
            // Supports both opened solution (.sln), project (.csproj) and folder.
            var solution = await VS.Solutions.GetCurrentSolutionAsync();

            return solution?.FullPath;
        }
    }
}
