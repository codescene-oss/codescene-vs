// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IWorkspaceWatchHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VisualStudioWorkspaceWatchHost : IWorkspaceWatchHost
    {
        private readonly IOpenDocumentContentProvider _contentProvider;

        [ImportingConstructor]
        public VisualStudioWorkspaceWatchHost(IOpenDocumentContentProvider contentProvider)
        {
            _contentProvider = contentProvider;
        }

        public IReadOnlyList<ReviewDocument> GetDirtyDocuments()
        {
            var documents = new List<ReviewDocument>();
            ThreadHelper.JoinableTaskFactory.Run(() => CollectDirtyDocumentsAsync(documents));
            return documents;
        }

        public void PruneMonitor(IReadOnlyList<string> repoRoots, ISet<string> keepPaths)
        {
            new DeltaCacheService().RemoveEntriesNotIn(keepPaths);
        }

        private static DTE2 TryGetDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var service = ServiceProvider.GlobalProvider.GetService(typeof(EnvDTE.DTE));
            if (service == null)
            {
                return null;
            }

            return service as DTE2;
        }

        private static bool IsDirtyDocument(EnvDTE.Document document)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (document == null || string.IsNullOrEmpty(document.FullName))
            {
                return false;
            }

            return !document.Saved;
        }

        private async Task CollectDirtyDocumentsAsync(List<ReviewDocument> documents)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var dte = TryGetDte();
            if (dte?.Documents == null)
            {
                return;
            }

            foreach (EnvDTE.Document document in dte.Documents)
            {
                await AddIfDirtyAsync(documents, document);
            }
        }

        private async Task AddIfDirtyAsync(List<ReviewDocument> documents, EnvDTE.Document document)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!IsDirtyDocument(document))
            {
                return;
            }

            var fullName = document.FullName;
            var content = await _contentProvider.GetContentForReviewAsync(fullName);
            documents.Add(new ReviewDocument
            {
                FilePath = fullName,
                Content = content ?? string.Empty,
                IsDirty = true,
            });
        }
    }
}
