// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Consts
{
    public class Constants
    {
        public class Timeout
        {
            public static readonly TimeSpan DEFAULTCLITIMEOUT = TimeSpan.FromMilliseconds(60000); // 60s
            public static readonly TimeSpan TELEMETRYTIMEOUT = TimeSpan.FromMilliseconds(5000); // 5s
            public static readonly TimeSpan SERVERSTARTUPTIMEOUT = TimeSpan.FromMilliseconds(30000);
        }

        public class Titles
        {
            public const string CODESCENE = "CodeScene";
            public const string CODESCENEAGENT = "CodeScene Agent";

            public const string REVIEW = "review";
            public const string DELTA = "delta";

            public const string BRAINCLASS = "Brain Class";
            public const string BRAINMETHOD = "Brain Method";
            public const string BUMPYROADAHEAD = "Bumpy Road Ahead";
            public const string CODEDUPLICATION = "Code Duplication";
            public const string COMPLEXCONDITIONAL = "Complex Conditional";
            public const string COMPLEXMETHOD = "Complex Method";
            public const string CONSTRUCTOROVERINJECTION = "Constructor Over-Injection";
            public const string DEEPGLOBALNESTEDCOMPLEXITY = "Deep Global Nested Complexity";
            public const string DEEPNESTEDCOMPLEXITY = "Deep, Nested Complexity";
            public const string DUPLICATEDASSERTIONBLOCKS = "Duplicated Assertion Blocks";
            public const string DUPLICATEDFUNCTIONBLOCKS = "Duplicated Function Blocks";
            public const string EXCESSNUMBEROFFUNCTIONARGUMENTS = "Excess Number of Function Arguments";
            public const string FILESIZEISSUE = "File Size Issue";
            public const string GLOBAlCONDITIONALS = "Global Conditionals";
            public const string HIGHDEGREEOFCODEDUPLICATION = "High Degree Of Code Duplication";
            public const string LARGEASSERTIONBLOCKS = "Large Assertion Blocks";
            public const string LARGEEMBEDDEDCODEBLOCK = "Large Embedded Code Block";
            public const string LARGEMETHOD = "Large Method";
            public const string LINESOFCODEINASINGLEFILE = "Lines Of Code In A Single File";
            public const string LINESOFDECLARATIONSINASINGLEFILE = "Lines Of Declarations In A Single File";
            public const string LOWCOHESION = "Low Cohesion";
            public const string MISSINGARGUMENTSABSTRACTIONS = "Missing Arguments Abstractions";
            public const string MODULARITYISSUE = "Modularity Issue";
            public const string NUMBEROFFUNCTIONSINASINGLEMODULE = "Number Of Functions In A Single Module";
            public const string OVERALLCODECOMPLEXITY = "Overall Code Complexity";
            public const string POTENTIALLYLOWCOHESION = "Potentially Low Cohesion";
            public const string PRIMITIVEOBSESSION = "Primitive Obsession";
            public const string STRINGHEAVYFUNCTIONARGUMENTS = "String Heavy Function Arguments";
            public const string CODEHEALTHMONITOR = "Code Health Monitor";
            public const string GENERALCODEHEALTH = "General Code Health";
            public const string ACCEPTTERMS = "Accept";
            public const string DECLINETERMS = "Decline";
            public const string VIEWTERMS = "View Terms & Policies";
            public const string TERMSINFO = "By using this extension you agree to CodeScene's Terms and Privacy Policy";
            public const string SETTINGSCOLLECTION = "CodeSceneExtension";
            public const string ACCEPTEDTERMSPROPERTY = "AcceptedTerms";
        }

        public class Telemetry
        {
            public const string SOURCEIDE = "vs";
            public const string SESSIONSTARTED = "session/started";
            public const string SESSIONENDED = "session/ended";

            public const string OPENDOCSPANEL = "docs/open-interactive-panel";

            public const string OPENSETTINGS = "control-center/open-settings";
            public const string OPENLINK = "control-center/open-link";

            public const string MONITORVISIBILITY = "code-health-monitor/visibility";

            public const string ANALYSISPERFORMANCE = "analysis/performance";

            public const string AIREFACTOR = "ai/refactor";
            public const string AISTAGEREQUESTED = "requested";
            public const string AISTAGEAGENTINVOKED = "agent-invoked";
            public const string AISTAGESUGGESTIONPRODUCED = "suggestion-produced";
            public const string AISTAGENOSUGGESTION = "no-suggestion";
            public const string AISTAGEPRESENTED = "presented";
            public const string AISTAGEDIFFSHOWN = "diff-shown";
            public const string AISTAGECOPIED = "copied";
            public const string AISTAGEAPPLIED = "applied";
            public const string AISTAGEDISCARDED = "discarded";
            public const string AISTAGEFAILED = "failed";
            public const string AISTAGECANCELLED = "cancelled";

            public const string REVOKETERMS = "terms/revoke";

            public const string REPOSITORYPATHS = "repository-paths";

            public const string AUTHSIGNINSTARTED = "auth/sign-in-started";
            public const string AUTHSIGNINCOMPLETED = "auth/sign-in-completed";
            public const string AUTHSIGNINCANCELLED = "auth/sign-in-cancelled";
            public const string AUTHSIGNINFAILED = "auth/sign-in-failed";
            public const string AUTHREAUTHORIZESTARTED = "auth/reauthorize-started";
            public const string AUTHREAUTHORIZEFAILED = "auth/reauthorize-failed";
            public const string AUTHSIGNOUT = "auth/sign-out";
            public const string AUTHPATSET = "auth/pat-set";
            public const string AUTHPATREMOVED = "auth/pat-removed";
        }
    }
}
