using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Cli
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
                        .SelectMany(fun => fun.CodeSmells.Select(smell => Map(filePath, fun, smell)))
                        .ToList() ?? new List<CodeSmellModel>(),
                };
            }
            catch (Exception ex)
            {
                var message = $"{ex.Message}\nPath:{filePath}";
                throw new InvalidOperationException(message);
            }
        }

        public CliCodeSmellModel Map(CodeSmellModel codeSmellModel)
        {
            return new CliCodeSmellModel
            {
                Category = codeSmellModel.Category,
                Details = codeSmellModel.Details,
                Range = new Models.Cli.CliRangeModel()
                {
                    StartLine = codeSmellModel.Range.StartLine,
                    StartColumn = codeSmellModel.Range.StartColumn,
                    EndLine = codeSmellModel.Range.EndLine,
                    EndColumn = codeSmellModel.Range.EndColumn
                },
            };
        }

        private CodeSmellModel Map(string path, CliCodeSmellModel review)
        {
            return new CodeSmellModel
            {
                Path = path,
                Category = review.Category,
                Details = review.Details,
                Range = new CodeRangeModel(
                    review.Range.StartLine,
                    review.Range.EndLine,
                    review.Range.StartColumn,
                    review.Range.EndColumn),
            };
        }

        private CodeSmellModel Map(string path, string functionName, CliCodeSmellModel review)
        {
            var model = Map(path, review);
            model.FunctionName = functionName;

            return model;
        }

        private CodeSmellModel Map(string path, CliReviewFunctionModel function, CliCodeSmellModel review)
        {
            var model = Map(path, review);
            model.FunctionName = function.Function;

            // Preserve the function's range information
            if (function.Range != null)
            {
                model.FunctionRange = new CodeRangeModel(
                    function.Range.StartLine,
                    function.Range.EndLine,
                    function.Range.StartColumn,
                    function.Range.EndColumn);
            }

            return model;
        }
    }
}
