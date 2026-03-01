// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Models.Git
{
    public sealed class ChangeDetectionContext
    {
        public ChangeDetectionContext(string gitRootPath, string workspacePath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver)
        {
            GitRootPath = gitRootPath;
            WorkspacePath = workspacePath;
            SavedFilesTracker = savedFilesTracker;
            OpenFilesObserver = openFilesObserver;
        }

        public string GitRootPath { get; }

        public string WorkspacePath { get; }

        public ISavedFilesTracker SavedFilesTracker { get; }

        public IOpenFilesObserver OpenFilesObserver { get; }
    }
}
