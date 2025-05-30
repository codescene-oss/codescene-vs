using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            try
            {
                return new FileReviewModel
                {
                    FilePath = filePath,
                    Score = result?.Score ?? 0,
                    FileLevel = result?.FileLevelCodeSmells?
                        .Select(x => Map(filePath, x))
                        .ToList() ?? new List<CodeSmellModel>(),
                    FunctionLevel = result?.FunctionLevelCodeSmells?
                        .SelectMany(x => (x?.CodeSmells ?? Array.Empty<CliCodeSmellModel>()))
                        .Select(y => Map(filePath, y))
                        .ToList() ?? new List<CodeSmellModel>()
                };
            }
            catch (Exception ex)
            {
                var r = JsonConvert.SerializeObject(result);
                var message = $"{ex.Message}\nPath:{filePath}\nReview:{r}";
                throw new InvalidOperationException(message);
            }
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