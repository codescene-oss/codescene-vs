// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Services
{
    public sealed class AnalysisBatchTracker
    {
        private int _total;
        private int _lastRemaining;

        public (int TotalCount, int RemainingCount) Update(int remaining)
        {
            if (remaining <= 0)
            {
                Reset();
                return (0, 0);
            }

            if (_total == 0)
            {
                _total = remaining;
            }
            else if (remaining > _lastRemaining)
            {
                _total += remaining - _lastRemaining;
            }

            _lastRemaining = remaining;
            return (_total, remaining);
        }

        public void Reset()
        {
            _total = 0;
            _lastRemaining = 0;
        }
    }
}
