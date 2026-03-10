// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Models.Git
{
    public sealed class ChangeDetectionContext
    {
        public ChangeDetectionContext(string gitRootPath, IReadOnlyCollection<string> workspacePaths, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            GitRootPath = gitRootPath;
            WorkspacePaths = workspacePaths ?? new string[0];
            SavedFilesTracker = savedFilesTracker;
            OpenFilesObserver = openFilesObserver;
        }

        public string GitRootPath { get; }

        public IReadOnlyCollection<string> WorkspacePaths { get; }

        public ISavedFilesTracker SavedFilesTracker { get; }

        public IOpenFilesObserver OpenFilesObserver { get; }
    }
}
