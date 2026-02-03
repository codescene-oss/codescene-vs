using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Mappers
{
    [Export(typeof(CodeHealthMonitorMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeHealthMonitorMapper
    {
        private readonly IPreflightManager _preflightManager;

        [ImportingConstructor]
        public CodeHealthMonitorMapper(IPreflightManager preflightManager)
        {
            _preflightManager = preflightManager;
        }

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
                AutoRefactor = _preflightManager.GetAutoRefactorConfig(),
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
                        ? new CodeRangeModel(
                            item.Function.Range.StartLine,
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
                        Range = item.RefactorableFn.Range != null
                            ? new CodeRangeModel(
                                item.RefactorableFn.Range.StartLine,
                                item.RefactorableFn.Range.EndLine,
                                item.RefactorableFn.Range.StartColumn,
                                item.RefactorableFn.Range.EndColumn)
                            : null,
                        NippyB64 = item.RefactorableFn.NippyB64,
                        FunctionType = item.RefactorableFn.FileType,
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
