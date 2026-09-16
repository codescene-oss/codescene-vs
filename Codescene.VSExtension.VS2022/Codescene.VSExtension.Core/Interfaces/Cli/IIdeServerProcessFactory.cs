// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerProcessFactory
    {
        IIdeServerProcess Start(string fileName, string arguments);
    }
}
