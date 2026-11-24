// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Mappers
{
    [Export(typeof(CodeSmellDocumentationMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeSmellDocumentationMapper
    {
        private readonly IAgentAvailability _availability;

        [ImportingConstructor]
        public CodeSmellDocumentationMapper(IAgentAvailability availability)
        {
            _availability = availability;
        }

        public CodeSmellDocumentationComponentData Map(ShowDocumentationModel model, FnToRefactorModel fnToRefactor)
        {
            var function = new FunctionModel
            {
                Name = model.FunctionName ?? string.Empty,
                Range = model.Range,
            };

            var autoRefactor = _availability.AutoRefactor();
            autoRefactor.Disabled = !_availability.Authenticated || fnToRefactor == null;
            autoRefactor.Activated = true;

            return new CodeSmellDocumentationComponentData
            {
                DocType = AddDocsPrefix(model.Category),
                AutoRefactor = autoRefactor,
                FileData = new FileDataModel
                {
                    FileName = model.Path,
                    Fn = function,
                    FnToRefactor = fnToRefactor,
                },
            };
        }

        private string AddDocsPrefix(string category)
        {
            // When opening docs from the monitor, the category is already formatted.
            if (category.Contains("docs_issues"))
            {
                return category;
            }
            else
            {
                return $"docs_issues_{TextUtils.ToSnakeCase(category)}";
            }
        }
    }
}
