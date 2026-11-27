// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.IO;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Application
{
    [Export(typeof(RefactorableFunctionEnricher))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class RefactorableFunctionEnricher
    {
        public void Enrich(DeltaResponseModel delta, string path, string content)
        {
            if (delta?.FunctionLevelFindings == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            foreach (var finding in delta.FunctionLevelFindings)
            {
                if (finding == null)
                {
                    continue;
                }

                finding.RefactorableFn = FunctionToRefactorResolver.ForFinding(path, content, finding);
            }
        }
    }
}
