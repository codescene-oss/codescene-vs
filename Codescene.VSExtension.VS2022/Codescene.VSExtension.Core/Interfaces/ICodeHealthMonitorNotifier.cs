// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface ICodeHealthMonitorNotifier
    {
        void OnDeltaStarting(string filePath);

        void OnDeltaCompleted(string filePath);
    }
}
