﻿using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.Mapper
{
    public interface IModelMapper
    {
        FileReviewModel Map(string filePath, CliReviewModel result);
    }
}
