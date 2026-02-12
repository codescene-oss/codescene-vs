// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli;

namespace Codescene.VSExtension.Core.Models.Ace
{
    /// <summary>
    /// Result of checking if a function has become stale in the document.
    /// </summary>
    public class StaleCheckResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether true if the function body can no longer be found in the document.
        /// </summary>
        public bool IsStale { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether true if the function was found at a different location.
        /// When true, <see cref="UpdatedRange"/> contains the new range.
        /// </summary>
        public bool RangeUpdated { get; set; }

        /// <summary>
        /// Gets or sets the updated range when the function was found at a different location.
        /// Only set when <see cref="RangeUpdated"/> is true.
        /// </summary>
        public CliRangeModel UpdatedRange { get; set; }
    }
}
