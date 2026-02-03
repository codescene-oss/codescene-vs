// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

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
