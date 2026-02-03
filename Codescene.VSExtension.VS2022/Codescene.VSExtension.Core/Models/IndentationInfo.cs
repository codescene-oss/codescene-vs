// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models
{
    /// <summary>
    /// Represents indentation information for a code block.
    /// </summary>
    public struct IndentationInfo
    {
        public int Level { get; set; }

        public bool UsesTabs { get; set; }

        public int TabSize { get; set; }
    }
}
