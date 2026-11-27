// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Agent
{
    public class AgentSkill
    {
        public AgentSkill(string name, string plugin, string inputFile, string outputFile)
        {
            Name = name;
            Plugin = plugin;
            InputFile = inputFile;
            OutputFile = outputFile;
        }

        public static AgentSkill RenderCodeFix { get; } = new AgentSkill(
            "render-code-fix",
            "render-code-fix-reporter",
            "render-code-fix-input.json",
            "render-code-fix-output.json");

        public static AgentSkill CheckConnectivity { get; } = new AgentSkill(
            "check-connectivity",
            "render-code-fix-reporter",
            null,
            null);

        public string Name { get; }

        public string Plugin { get; }

        public string InputFile { get; }

        public string OutputFile { get; }
    }
}
