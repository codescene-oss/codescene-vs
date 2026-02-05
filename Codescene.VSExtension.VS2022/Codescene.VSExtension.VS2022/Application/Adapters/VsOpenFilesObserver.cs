// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IOpenFilesObserver))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsOpenFilesObserver : IOpenFilesObserver
    {
        [Import]
        private IOpenFilesSource _source;

        [Import]
        private ILogger _logger;

        private OpenFilesObserverCore _core;

        private OpenFilesObserverCore Core
        {
            get
            {
                if (_core == null)
                {
                    _core = new OpenFilesObserverCore(_source, _logger);
                }

                return _core;
            }
        }

        public IEnumerable<string> GetAllVisibleFileNames()
        {
            return Core.GetAllVisibleFileNames();
        }
    }
}
