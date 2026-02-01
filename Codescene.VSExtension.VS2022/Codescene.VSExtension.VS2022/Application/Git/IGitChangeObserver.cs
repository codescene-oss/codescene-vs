using Codescene.VSExtension.Core.Interfaces.Git;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    public interface IGitChangeObserver : IDisposable
    {
        void Initialize(string solutionPath, ISavedFilesTracker savedFilesTracker, IOpenFilesObserver openFilesObserver);
        void Start();
        Task<List<string>> GetChangedFilesVsBaselineAsync();
        void RemoveFromTracker(string filePath);
        event EventHandler<string> FileDeletedFromGit;
    }
}
