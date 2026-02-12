// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Consts
{
    public class WebComponentConstants
    {
        public const string VISUALSTUDIOIDETYPE = "Visual Studio";

        public class ViewTypes
        {
            public const string ACE = "ace";
            public const string DOCS = "docs";
            public const string HOME = "home"; // Code Health Monitor
            public const string ACEACKNOWLEDGE = "aceAcknowledge";
        }

        public class AceViewErrorTypes
        {
            public const string GENERIC = "generic";
            public const string AUTH = "auth";
        }

        public class StateTypes
        {
            public const string RUNNING = "running";
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
            public const string GOTOFUNCTIONLOCATION = "goto-function-location";
            public const string COPYCODE = "copyCode";
            public const string SHOWLOGOUTPUT = "showLogoutput";
            public const string SHOWDIFF = "showDiff";
            public const string REQUESTANDPRESENTREFACTORING = "request-and-present-refactoring";
            public const string UPDATERENDERER = "update-renderer";
            public const string OPENDOCSFORFUNCTION = "open-docs-for-function";
            public const string CANCEL = "cancel";
            public const string OPENSETTINGS = "open-settings";
        }
    }
}
