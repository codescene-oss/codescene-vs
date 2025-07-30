using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.Util;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.WebComponent
{
    [Export(typeof(CodeHealthMonitorMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeHealthMonitorMapper
    {
        public CodeHealthMonitorComponentData Map(Dictionary<string, DeltaResponseModel> fileDeltas)
        {
            var files = fileDeltas.Select(pair => new FileDeltaData
            {
                File = new File
                {
                    FileName = pair.Key
                },
                Delta = new Delta
                {
                    ScoreChange = pair.Value.ScoreChange,
                    OldScore = pair.Value.OldScore,
                    NewScore = pair.Value.NewScore,
                    FileLevelFindings = ToChangeDetails(pair.Value.FileLevelFindings),
                    FunctionLevelFindings = ToFunctionFinding(pair.Value.FunctionLevelFindings),
                }
            }).ToList();

            return new CodeHealthMonitorComponentData
            {
                AutoRefactor = new AutoRefactorConfig {Activated = true, Visibile = true, Disabled = false},
                FileDeltaData = files,
                Jobs = DeltaJobTracker.RunningJobs.ToList()
                
            };
        }

        private List<FunctionFinding> ToFunctionFinding(FunctionFindingModel[] model)
        {
            var result = new List<FunctionFinding>();

            if (model == null)
                return result;

            foreach (var item in model)
            {
                var function = new Function
                {
                    Name = item.Function?.Name,
                    Range = item.Function?.Range != null
                        ? new CodeSmellRangeModel(
                            item.Function.Range.Startline,
                            item.Function.Range.EndLine,
                            item.Function.Range.StartColumn,
                            item.Function.Range.EndColumn)
                        : null
                };

                var refactorableFn = item.RefactorableFn != null
                    ? new FunctionToRefactor
                    {
                        Name = item.RefactorableFn.Name,
                        Body = item.RefactorableFn.Body,
                        //FunctionType = item.RefactorableFn.FunctionType,
                        Range = item.RefactorableFn.Range != null
                            ? new CodeSmellRangeModel(
                                item.RefactorableFn.Range.Startline,
                                item.RefactorableFn.Range.EndLine,
                                item.RefactorableFn.Range.StartColumn,
                                item.RefactorableFn.Range.EndColumn)
                            : null,
                        RefactoringTargets = item.RefactorableFn.RefactoringTargets?.ToList()
                    }
                    : null;

                result.Add(new FunctionFinding
                {
                    Function = function,
                    ChangeDetails = ToChangeDetails(item.ChangeDetails),
                    RefactorableFn = refactorableFn
                });
            }

            return result;
        }

        private List<ChangeDetail> ToChangeDetails(ChangeDetailModel[] model)
        {
            if (model == null)
                return new List<ChangeDetail>();

            return model.Select(item => new ChangeDetail
            {
                Line = item.Line,
                Description = item.Description,
                ChangeType = item.ChangeType,
                Category = item.Category
            }).ToList();
        }
    }
}
