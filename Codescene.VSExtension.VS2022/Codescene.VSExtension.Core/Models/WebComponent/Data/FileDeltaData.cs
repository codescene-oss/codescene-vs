// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{

    public class FileDeltaData
    {
        public File File { get; set; }

        public Delta Delta { get; set; }
    }
}
