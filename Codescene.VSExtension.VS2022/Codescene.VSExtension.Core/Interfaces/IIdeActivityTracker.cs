// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces
{
    public interface IIdeActivityTracker
    {
        bool IsIdeWindowActive();

        void SetActiveForTesting(bool active);
    }
}
