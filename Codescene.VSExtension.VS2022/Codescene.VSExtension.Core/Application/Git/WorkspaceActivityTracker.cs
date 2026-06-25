// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class WorkspaceActivityTracker
    {
        private static bool _activitySinceLastScan;

        public static void MarkActivity()
        {
            _activitySinceLastScan = true;
        }

        public static bool ConsumeActivity()
        {
            var hadActivity = _activitySinceLastScan;
            _activitySinceLastScan = false;
            return hadActivity;
        }

        public static void Reset()
        {
            _activitySinceLastScan = false;
        }
    }
}
