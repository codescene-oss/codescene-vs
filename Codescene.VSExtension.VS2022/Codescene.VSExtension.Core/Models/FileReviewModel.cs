using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.ReviewModels
{
    public class FileReviewModel
    {
        public string RawScore { get; set; }
        public string FilePath { get; set; }
        public float Score { get; set; }
        //public List<ReviewModel> ExpressionLevel { get; set; } = new List<ReviewModel>();
        public List<CodeSmellModel> FileLevel { get; set; }
        public List<CodeSmellModel> FunctionLevel { get; set; }
    }
}
