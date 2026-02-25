// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(IGitChangeLister))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GitChangeListerService : GitChangeLister
    {
        [ImportingConstructor]
        public GitChangeListerService(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService)
            : base(savedFilesTracker, supportedFileChecker, logger, gitService)
        {
        }
    }
}
