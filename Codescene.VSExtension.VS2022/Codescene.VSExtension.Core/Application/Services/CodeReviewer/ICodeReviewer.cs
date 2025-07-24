using Codescene.VSExtension.Core.Models.ReviewModels;
using System.Collections;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface ICodeReviewer
    {
		FileReviewModel Review(string path, string content);
		DeltaResponseModel Delta(FileReviewModel review, string currentCode);
	}
}
