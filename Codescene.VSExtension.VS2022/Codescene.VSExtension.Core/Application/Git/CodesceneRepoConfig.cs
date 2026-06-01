// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class CodesceneRepoConfig
    {
        public static string GetBaselineBranch(string gitRootPath)
        {
            if (string.IsNullOrEmpty(gitRootPath))
            {
                return null;
            }

            var configPath = Path.Combine(
                gitRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                CodesceneFileWatcher.CodesceneDir,
                CodesceneFileWatcher.ConfigFileName);

            if (!File.Exists(configPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var model = JsonConvert.DeserializeObject<CodesceneConfigModel>(json);
                var branch = model?.BaselineBranch?.Trim();
                return string.IsNullOrEmpty(branch) ? null : branch;
            }
            catch
            {
                return null;
            }
        }

        private sealed class CodesceneConfigModel
        {
            [JsonProperty("baseline_branch")]
            public string BaselineBranch { get; set; }
        }
    }
}
