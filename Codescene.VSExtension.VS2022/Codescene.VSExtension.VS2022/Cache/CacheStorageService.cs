using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Cache
{
    [Export(typeof(ICacheStorageService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CacheStorageService : ICacheStorageService
    {
        private string _cachePath;
        private bool _initialized;

        private const string REVIEWRESULTSFOLDER = ".review-results";

        public async Task InitializeAsync()
        {
            if (_initialized) return;
            _initialized = true;

            await UpdateCachePathAsync();

            // Subscribe to solution/folder events
            VS.Events.SolutionEvents.OnAfterOpenSolution += _ => UpdateCachePathAsync().FireAndForget();
            VS.Events.SolutionEvents.OnAfterCloseSolution += () => _ = UpdateCachePathAsync();
            VS.Events.SolutionEvents.OnAfterOpenFolder += _ => UpdateCachePathAsync().FireAndForget();
            VS.Events.SolutionEvents.OnAfterCloseFolder += _ => UpdateCachePathAsync().FireAndForget();
        }
        private async Task UpdateCachePathAsync()
        {
            string workspaceId = await GetWorkspaceIdentifierAsync();

            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Codescene");

            if (!string.IsNullOrEmpty(workspaceId))
            {
                string hash = ComputeHash(workspaceId);
                _cachePath = Path.Combine(basePath, "WorkspaceCache", hash);
            }
            else
            {
                _cachePath = null;  // No workspace open
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

        private static string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToLowerInvariant()));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 16);
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
    }
}
