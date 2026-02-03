// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class CodeHealthMonitorComponentData
    {
        public bool ShowOnboarding { get; set; } = false;

        public AutoRefactorConfig AutoRefactor { get; set; }

        public List<FileDeltaData> FileDeltaData { get; set; }

        public List<Job> Jobs { get; set; }
    }
}
