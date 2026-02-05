// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IOpenFilesSource
    {
        IEnumerable<string> GetOpenDocumentPaths();
    }
}
