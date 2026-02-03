using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models
{
    public class FileReviewModel
    {
        public string RawScore { get; set; }
        public string FilePath { get; set; }
        public float Score { get; set; }
        public List<CodeSmellModel> FileLevel { get; set; }
        public List<CodeSmellModel> FunctionLevel { get; set; }
    }
}
