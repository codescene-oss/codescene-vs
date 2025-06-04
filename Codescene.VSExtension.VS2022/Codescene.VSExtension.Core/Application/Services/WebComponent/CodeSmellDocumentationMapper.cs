using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Util;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Services.WebComponent
{
    [Export(typeof(CodeSmellDocumentationMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeSmellDocumentationMapper
    {
        public CodeSmellDocumentationComponentData Map(ShowDocumentationModel model)
        {
            var function = new FunctionModel
            {
                Name = model.FunctionName,
                Range = new RangeModel(
                    model.Range.StartLine,
                    model.Range.EndLine,
                    model.Range.StartColumn,
                    model.Range.EndColumn
                )
            };

            return new CodeSmellDocumentationComponentData
            {
                DocType = $"docs_issues_{TextUtils.ToSnakeCase(model.Category)}",
                AutoRefactor = new AutoRefactorModel
                {
                    Activated = false,
                    Disabled = true,
                    Visible = false
                },
                FileData = new FileDataModel
                {
                    Filename = Path.GetFileName(model.Path),
                    Fn = function,
                    Action = new ActionModel
                    {
                        GoToFunctionLocationPayload = new GoToFunctionLocationPayloadModel
                        {
                            Filename = Path.GetFileName(model.Path),
                            Fn = function,
                        }
                    }
                }
            };
        }
    }
}
