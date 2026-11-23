// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentProcessStartInfo
    {
        public string FileName { get; set; }

        public string Arguments { get; set; }

        public string WorkingDirectory { get; set; }

        public IReadOnlyDictionary<string, string> EnvironmentAdditions { get; set; }
    }
}
