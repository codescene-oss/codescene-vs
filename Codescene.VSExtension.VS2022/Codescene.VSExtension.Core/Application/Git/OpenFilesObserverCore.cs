// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class OpenFilesObserverCore : IOpenFilesObserver
    {
        private readonly IOpenFilesSource _source;
        private readonly ILogger _logger;

        public OpenFilesObserverCore(IOpenFilesSource source, ILogger logger)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<string> GetAllVisibleFileNames()
        {
            try
            {
                var paths = _source.GetOpenDocumentPaths();
                if (paths == null)
                {
                    _logger.Warn("OpenFilesObserverCore: Source returned null");
                    return Enumerable.Empty<string>();
                }

                return paths
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Where(path => Path.IsPathRooted(path))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.Error("OpenFilesObserverCore: Exception getting open files", ex);
                return Enumerable.Empty<string>();
            }
        }
    }
}
