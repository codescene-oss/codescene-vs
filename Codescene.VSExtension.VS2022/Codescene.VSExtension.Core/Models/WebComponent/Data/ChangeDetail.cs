// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Enums;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class ChangeDetail
    {
        public int? Line { get; set; }

        public string Description { get; set; }

        public ChangeType ChangeType { get; set; }

        public string Category { get; set; }
    }
}
