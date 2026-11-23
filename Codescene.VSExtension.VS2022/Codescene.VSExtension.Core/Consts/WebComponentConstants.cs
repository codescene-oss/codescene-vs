// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Consts
{
    public class WebComponentConstants
    {
        public const string VISUALSTUDIOIDETYPE = "Visual Studio";

        public class ViewTypes
        {
            public const string ACE = "ace";
            public const string AUTH = "auth";
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
            public const string STOPANALYSIS = "stop-analysis";
            public const string STARTANALYSIS = "start-analysis";
            public const string OPENAUTH = "open-auth";
            public const string AUTHCLOSE = "auth-close";
            public const string AUTHSKIPPROJECT = "auth-skip-project";
            public const string HIDESIGNINBANNER = "hide-sign-in-banner";
            public const string AUTHSIGNIN = "auth-sign-in";
            public const string AUTHOPENBROWSERAGAIN = "auth-open-browser-again";
            public const string AUTHSIGNOUT = "auth-sign-out";
            public const string AUTHRETRY = "auth-retry";
            public const string AUTHSELECTACCOUNT = "auth-select-account";
            public const string AUTHLINKPROJECT = "auth-link-project";
            public const string AUTHSETACCESSTOKEN = "auth-set-access-token";
            public const string AUTHREMOVEACCESSTOKEN = "auth-remove-access-token";
            public const string AUTHOPENSERVERSETTING = "auth-open-server-setting";
            public const string AUTHOPENPATPAGE = "auth-open-pat-page";
            public const string AUTHREAUTHORIZE = "auth-reauthorize";
        }
    }
}
