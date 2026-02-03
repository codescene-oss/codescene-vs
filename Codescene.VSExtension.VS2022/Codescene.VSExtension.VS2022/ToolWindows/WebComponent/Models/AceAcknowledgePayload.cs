// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;

public class AceAcknowledgePayload
{
    public string FilePath { get; set; }

    public FnToRefactorModel FnToRefactor { get; set; }

    public string Source { get; set; }
}
