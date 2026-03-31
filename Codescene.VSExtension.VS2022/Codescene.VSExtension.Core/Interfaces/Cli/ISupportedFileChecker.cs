// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ISupportedFileChecker
    {
        bool IsSupported(string filePath);
    }
}
