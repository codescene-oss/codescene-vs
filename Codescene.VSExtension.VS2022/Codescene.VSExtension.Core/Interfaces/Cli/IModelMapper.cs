using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IModelMapper
    {
        FileReviewModel Map(string filePath, CliReviewModel result);
        CliCodeSmellModel Map(CodeSmellModel codeSmellModel);
    }
}
