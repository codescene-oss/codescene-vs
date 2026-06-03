// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Util
{
    public static class AutoRefactorConfigUtils
    {
        public static bool ComputeActivated(bool aceAcknowledged, bool hasToken) =>
            !(!aceAcknowledged && hasToken);
    }
}
