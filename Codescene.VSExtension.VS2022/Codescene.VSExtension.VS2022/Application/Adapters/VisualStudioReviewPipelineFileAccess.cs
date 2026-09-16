// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IReviewPipelineFileAccess))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VisualStudioReviewPipelineFileAccess : IReviewPipelineFileAccess
    {
        private readonly IOpenDocumentContentProvider _contentProvider;
        private readonly IOpenFilesObserver _openFilesObserver;

        [ImportingConstructor]
        public VisualStudioReviewPipelineFileAccess(
            IOpenDocumentContentProvider contentProvider,
            IOpenFilesObserver openFilesObserver)
        {
            _contentProvider = contentProvider;
            _openFilesObserver = openFilesObserver;
        }

        public ReviewDocument FindOpenDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var content = ThreadHelper.JoinableTaskFactory.Run(() => _contentProvider.GetContentForReviewAsync(filePath));
            if (content == null)
            {
                return null;
            }

            return new ReviewDocument
            {
                FilePath = filePath,
                Content = content,
                IsDirty = true,
            };
        }

        public Task<ReviewDocument> OpenDocumentAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return Task.FromResult<ReviewDocument>(null);
                }

                return Task.FromResult(new ReviewDocument
                {
                    FilePath = filePath,
                    Content = File.ReadAllText(filePath),
                    IsDirty = false,
                });
            }
            catch
            {
                return Task.FromResult<ReviewDocument>(null);
            }
        }

        public Task<byte[]> ReadFileBytesAsync(string filePath)
        {
            try
            {
                return Task.FromResult(File.Exists(filePath) ? File.ReadAllBytes(filePath) : null);
            }
            catch
            {
                return Task.FromResult<byte[]>(null);
            }
        }

        public bool IsVisible(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            return _openFilesObserver.GetAllVisibleFileNames()
                .Any(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
