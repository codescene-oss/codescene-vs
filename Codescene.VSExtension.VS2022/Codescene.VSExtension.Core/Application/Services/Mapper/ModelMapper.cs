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
                    RawScore = result?.RawScore,
                    FilePath = filePath,
                    Score = result?.Score ?? 0,
                    FileLevel = result?.FileLevelCodeSmells?
                        .Select(smell => Map(filePath, smell))
                        .ToList() ?? new List<CodeSmellModel>(),
                    FunctionLevel = result?.FunctionLevelCodeSmells?
                        .Where(fun => fun.CodeSmells != null)
                        .SelectMany(fun => fun.CodeSmells.Select(smell => Map(filePath, fun.Function, smell)))
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
                Range = new CodeSmellRangeModel(
                    review.Range.Startline,
                    review.Range.EndLine,
                    review.Range.StartColumn,
                    review.Range.EndColumn
                )
            };
        }

        private CodeSmellModel Map(string path, string functionName, CliCodeSmellModel review)
        {
            var model = Map(path, review);
            model.FunctionName = functionName;

            return model;
        }
    }
}