// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal static class AgentInputBuilder
    {
        public static AgentInput ForRefactoring(string taskId, string path, FnToRefactorModel fn)
        {
            if (fn.RefactoringTargets == null || fn.RefactoringTargets.Length == 0)
            {
                throw new AgentFailure(AgentFailureKind.Generic, "No refactoring targets available");
            }

            var firstTarget = fn.RefactoringTargets[0];

            return new AgentInput
            {
                Context = new JObject
                {
                    ["@vocab"] = "https://codescene.io/schemas/code-health-fix#",
                },
                TaskId = taskId,
                File = path,
                Line = firstTarget.Line,
                Smell = firstTarget.Category,
            };
        }
    }
}
