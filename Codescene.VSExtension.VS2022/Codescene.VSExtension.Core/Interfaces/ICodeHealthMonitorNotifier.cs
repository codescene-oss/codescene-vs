// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface ICodeHealthMonitorNotifier
    {
        void OnDeltaStarting(string filePath);

        void OnDeltaCompleted(string filePath);

        void RequestViewUpdate();

        void ApplyQueue(ReviewQueue queue);

        void Clear();
    }
}
