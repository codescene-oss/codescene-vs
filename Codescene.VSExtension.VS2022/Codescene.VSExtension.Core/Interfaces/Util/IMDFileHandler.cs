// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    public interface IMDFileHandler
    {
        string GetContent(string path, string subPath);

        void SetFileName(string fileName);
    }
}
