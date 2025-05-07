using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.WebComponent
{
    [Export(typeof(WebComponentMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class WebComponentMapper
    {
        public WebComponentData Map(RefactorResponseModel refactoredCode)
        {
            var data = new WebComponentData
            {
                Loading = false,
                FileData = new WebComponentFileData
                {
                    Filename = "FileData Level FileName",
                    Fn = new WebComponentFileDataBaseFn
                    {
                        Name = "FileData Level Fn Function",
                        Range = new CliRangeModel
                        {
                            Startline = 1,
                            StartColumn = 2,
                            EndLine = 3,
                            EndColumn = 4
                        }
                    },
                    Action = new WebComponentAction
                    {
                        GoToFunctionLocationPayload = new WebComponentFileDataBase
                        {
                            Filename = "Action Level FileName",
                            Fn = new WebComponentFileDataBaseFn
                            {
                                Name = "Action Level Fn Name",
                                Range = new CliRangeModel
                                {
                                    Startline = 11,
                                    StartColumn = 22,
                                    EndLine = 33,
                                    EndColumn = 44
                                }
                            }
                        }
                    }
                },
                AceResultData = refactoredCode
            };

            return data;

        }
    }
}
