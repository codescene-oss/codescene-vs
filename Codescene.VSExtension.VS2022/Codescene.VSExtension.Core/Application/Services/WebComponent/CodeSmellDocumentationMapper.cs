using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Util;
using System.ComponentModel.Composition;

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
                Name = model.FunctionName ?? "",
                Range = new CodeSmellRangeModel(
                    model.Range?.StartLine ?? 1,
                    model.Range?.EndLine ?? 1,
                    model.Range?.StartColumn ?? 1,
                    model.Range?.EndColumn ?? 1
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
                    FileName = model.Path,
                    Fn = function,
                }
            };
        }
    }
}
