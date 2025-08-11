namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentConstants
    {
        public const string VISUAL_STUDIO_IDE_TYPE = "Visual Studio";

        public class ViewTypes
        {
            public const string DOCS = "docs";
        }

        public class MessageTypes
        {
            public const string INIT = "init";
            public const string APPLY = "apply";
            public const string ACKNOWLEDGED = "acknowledged";
            public const string REJECT = "reject";
            public const string RETRY = "retry";
            public const string CLOSE = "close";
            public const string GOTO_FUNCTION_LOCATION = "goto-function-location";
            public const string COPY_CODE = "copyCode";
            public const string SHOW_LOG_OUTPUT = "showLogoutput";
            public const string OPEN_FILE = "open-file";
            public const string OPEN_FUNCTION = "open-function";
            public const string SHOW_DIFF = "showDiff";
            public const string REQUEST_AND_PRESENT_REFACTORING = "request-and-present-refactoring";
            public const string UPDATE_RENDERER = "update-renderer";
            public const string OPEN_EXTERNAL_LINK = "open-external-link";
        }
    }
}
