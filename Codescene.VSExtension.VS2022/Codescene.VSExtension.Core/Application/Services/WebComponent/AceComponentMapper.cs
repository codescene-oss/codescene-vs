using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Services.WebComponent
{
    [Export(typeof(AceComponentMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceComponentMapper
    {
        public AceComponentData Map(CachedRefactoringActionModel model)
        {
            var data = new AceComponentData
            {
                Loading = false,
                FileData = new WebComponentFileData
                {
                    FileName = model.Path,
                    Fn = new WebComponentFileDataBaseFn
                    {
                        Name = model.RefactorableCandidate.Name,
                        Range = new CliRangeModel
                        {
                            Startline = model.RefactorableCandidate.Range.Startline,
                            StartColumn = model.RefactorableCandidate.Range.StartColumn,
                            EndLine = model.RefactorableCandidate.Range.EndLine,
                            EndColumn = model.RefactorableCandidate.Range.EndColumn
                        }
                    },
                    Action = new WebComponentAction
                    {
                        GoToFunctionLocationPayload = new WebComponentFileDataBase
                        {
                            FileName = model.Path,
                            Fn = new WebComponentFileDataBaseFn
                            {
                                Name = model.RefactorableCandidate.Name,
                                Range = new CliRangeModel
                                {
                                    Startline = model.RefactorableCandidate.Range.Startline,
                                    StartColumn = model.RefactorableCandidate.Range.StartColumn,
                                    EndLine = model.RefactorableCandidate.Range.EndLine,
                                    EndColumn = model.RefactorableCandidate.Range.EndColumn
                                }
                            }
                        }
                    }
                },
                AceResultData = model.Refactored
            };

            return data;
        }

		public AceComponentData Map(string path, FnToRefactorModel model)
		{
			var data = new AceComponentData
			{
				Loading = true,
				FileData = new WebComponentFileData
				{
					FileName = path,
					Fn = new WebComponentFileDataBaseFn
					{
						Name = model.Name,
						Range = new CliRangeModel
						{
							Startline = model.Range.Startline,
							StartColumn = model.Range.StartColumn,
							EndLine = model.Range.EndLine,
							EndColumn = model.Range.EndColumn
						}
					},
					Action = new WebComponentAction
					{
						GoToFunctionLocationPayload = new WebComponentFileDataBase
						{
							FileName = path,
							Fn = new WebComponentFileDataBaseFn
							{
								Name = model.Name,
								Range = new CliRangeModel
								{
									Startline = model.Range.Startline,
									StartColumn = model.Range.StartColumn,
									EndLine = model.Range.EndLine,
									EndColumn = model.Range.EndColumn
								}
							}
						}
					}
				},
				AceResultData = null
			};

			return data;
		}
    }
}
