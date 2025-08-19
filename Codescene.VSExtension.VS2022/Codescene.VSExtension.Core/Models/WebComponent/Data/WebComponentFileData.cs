using Codescene.VSExtension.Core.Models.Cli;

namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentFileDataBase
    {
        public string FileName { get; set; }
        public WebComponentFileDataBaseFn Fn { get; set; }
    }

    public class WebComponentFileDataBaseFn
    {
        public string Name { get; set; }
        public CliRangeModel Range { get; set; }
    }

    public class WebComponentFileData : WebComponentFileDataBase
    {
        public WebComponentAction Action { get; set; }
    }

    public class WebComponentAction
    {
        public WebComponentFileDataBase GoToFunctionLocationPayload { get; set; }
    }
}
