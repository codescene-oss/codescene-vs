using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
        FileReviewModel Review(string path);
        void UseFileOnPathType();
        void UseContentOnlyType(string content);
        List<CodeSmellModel> GetCodesmellExpressions(string path);
    }
}
