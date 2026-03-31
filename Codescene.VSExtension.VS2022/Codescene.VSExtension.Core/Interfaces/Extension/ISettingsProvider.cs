// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Extension
{
    public interface ISettingsProvider
    {
        bool ShowDebugLogs { get; }

        string AuthToken { get; }
    }
}
