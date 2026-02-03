using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Util;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Mappers
{
    [Export(typeof(CodeSmellDocumentationMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeSmellDocumentationMapper
    {
        private readonly ISettingsProvider _settingsProvider;

        [ImportingConstructor]
        public CodeSmellDocumentationMapper(ISettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider;
        }


        public CodeSmellDocumentationComponentData Map(ShowDocumentationModel model, FnToRefactorModel fnToRefactor, bool aceAcknowledged = false)
        {
            var function = new FunctionModel
            {
                Name = model.FunctionName ?? "",
                Range = new CodeRangeModel(
                    model.Range?.StartLine ?? 1,
                    model.Range?.EndLine ?? 1,
                    model.Range?.StartColumn ?? 1,
                    model.Range?.EndColumn ?? 1
                )
            };

            var hasToken = !string.IsNullOrWhiteSpace(_settingsProvider.AuthToken);

            return new CodeSmellDocumentationComponentData
            {
                DocType = AddDocsPrefix(model.Category),
                AutoRefactor = new AutoRefactorConfig
                {
                    Visible = true,
                    Activated = aceAcknowledged,
                    Disabled = !hasToken || fnToRefactor == null,
                },
                FileData = new FileDataModel
                {
                    FileName = model.Path,
                    Fn = function,
                    FnToRefactor = fnToRefactor
                }
            };
        }

        private string AddDocsPrefix(string category)
        {
            if (category.Contains("docs_issues")) // When opening docs from the monitor, the category is already formatted.
                return category;
            else
                return $"docs_issues_{TextUtils.ToSnakeCase(category)}";
        }
    }
}
