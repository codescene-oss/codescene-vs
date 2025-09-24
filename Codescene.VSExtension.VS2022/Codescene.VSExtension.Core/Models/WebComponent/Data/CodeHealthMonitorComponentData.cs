using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class CodeHealthMonitorComponentData
    {
        public bool ShowOnboarding { get; set; } = false;
        public AutoRefactorConfig AutoRefactor { get; set; }
        public List<FileDeltaData> FileDeltaData { get; set; }
        public List<Job> Jobs { get; set; }
    }

    public class FileDeltaData
    {
        public File File { get; set; }
        public Delta Delta { get; set; }
    }

    public class File
    {
        public string FileName { get; set; } // Absolute path
    }

    public class Delta
    {
        public decimal ScoreChange { get; set; }
        public decimal NewScore { get; set; }
        public decimal OldScore { get; set; }
        public List<ChangeDetail> FileLevelFindings { get; set; }
        public List<FunctionFinding> FunctionLevelFindings { get; set; }
    }

    public class AutoRefactorConfig
    {
        public bool Activated { get; set; }
        public bool Visibile { get; set; }
        public bool Disabled { get; set; }
    }

    public class ChangeDetail
    {
        public int? Line { get; set; }
        public string Description { get; set; }
        public ChangeType ChangeType { get; set; }
        public string Category { get; set; }
    }

    public class FunctionFinding
    {
        public Function Function { get; set; }
        public List<ChangeDetail> ChangeDetails { get; set; }
        public FunctionToRefactor RefactorableFn { get; set; }
    }

    public class Function
    {
        public string Name { get; set; }
        public CodeSmellRangeModel Range { get; set; }
    }

    public class FunctionToRefactor
    {
        public string Name { get; set; }
        public string Body { get; set; }
        public string FunctionType { get; set; }
        public CodeSmellRangeModel Range { get; set; }
        public List<RefactoringTargetModel> RefactoringTargets { get; set; }
    }
}
