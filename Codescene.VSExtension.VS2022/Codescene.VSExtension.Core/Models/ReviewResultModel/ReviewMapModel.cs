using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.ReviewResultModel
{
    public class ReviewMapModel
    {
        public float Score { get; set; }
        public List<ReviewModel> ExpressionLevel { get; set; } = new List<ReviewModel>();
        public List<ReviewModel> FileLevel { get; set; }
        public List<ReviewModel> FunctionLevel { get; set; }

    }
}
