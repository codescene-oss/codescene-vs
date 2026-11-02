// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Util
{
    public static class DeltaScoreText
    {
        public static string Format(DeltaResponseModel delta)
        {
            if (delta == null)
            {
                return null;
            }

            if (!delta.NewScore.HasValue)
            {
                return null;
            }

            if (!delta.OldScore.HasValue)
            {
                return delta.NewScore.Value.ToString();
            }

            return delta.OldScore.Value + " → " + delta.NewScore.Value;
        }
    }
}
