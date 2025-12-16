namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentConstants
    {
        public const string VISUAL_STUDIO_IDE_TYPE = "Visual Studio";

        public class ViewTypes
        {
			public const string ACE = "ace";
			public const string DOCS = "docs";
            public const string HOME = "home"; // Code Health Monitor
        }

        public class AceViewErrorTypes
        {
            public const string GENERIC = "generic";
            public const string AUTH = "auth";
        }

        public class StateTypes
        {
            public const string RUNNING = "running";
            public const string QUEUED = "queued";
        }

        public class JobTypes
        {
            public const string DELTA = "deltaAnalysis";
            public const string ACE = "autoRefactor";
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
            public const string OPEN_DOCS_FOR_FUNCTION = "open-docs-for-function";
            public const string CANCEL = "cancel";
            public const string OPEN_SETTINGS = "open-settings";
        }
    }
}
