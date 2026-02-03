// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.WebComponent.Model
{
    public class ShowDocumentationModel
    {
        public ShowDocumentationModel(string path, string category, string functionName, CodeRangeModel range)
        {
            Path = path;
            Category = category;
            FunctionName = functionName;
            Range = range;
        }

        public string Path { get; set; }

        public string Category { get; set; }

        public string FunctionName { get; set; }

        public CodeRangeModel Range { get; set; }
    }
}
