using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System.ComponentModel.Composition;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.Mapper
{
    [Export(typeof(IModelMapper))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ModelMapper : IModelMapper
    {
        public FileReviewModel Map(string filePath, CliReviewModel result)
        {
            return new FileReviewModel
            {
                FilePath = filePath,
                Score = result.Score ?? 0,
                FileLevel = result.FileLevelCodeSmells.Select(x => Map(filePath, x)).ToList(),
                FunctionLevel = result.FunctionLevelCodeSmells.SelectMany(x => x.CodeSmells.Select(y => Map(filePath, y))).ToList()
            };
        }

        private CodeSmellModel Map(string path, CliCodeSmellModel review)
        {
            return new CodeSmellModel
            {
                Path = path,
                Category = review.Category,
                Details = review.Details,
                StartLine = review.Range.Startline,
                EndLine = review.Range.EndLine,
                StartColumn = review.Range.StartColumn,
                EndColumn = review.Range.EndColumn
            };
        }
    }
}