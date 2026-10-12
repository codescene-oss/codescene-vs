// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.VS2022.Application.Cli
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CachingCodeReviewerProvider : ICodeReviewer
    {
        private readonly ICodeReviewer _inner;

        [ImportingConstructor]
        public CachingCodeReviewerProvider(
            ILogger logger,
            IModelMapper mapper,
            ICliExecutor executor,
            IPreflightManager preflightManager,
            IReviewPipeline pipeline,
            ISettingsProvider settings)
        {
            _inner = new CodeReviewer(logger, mapper, executor, pipeline, preflightManager, settings);
        }

        public Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            return _inner.ReviewWithDeltaAsync(path, content, cancellationToken);
        }
    }
}
