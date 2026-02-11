// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class AutoRefactorConfig
    {
        public bool Activated { get; set; }

        public bool Visible { get; set; }

        public bool Disabled { get; set; }

        public AceStatusType AceStatus { get; set; }
    }
}
