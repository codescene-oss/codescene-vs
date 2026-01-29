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
        }

        public class Titles
        {
            public const string CODESCENE = "CodeScene";
            public const string CODESCENEACE = "CodeScene ACE";

            public const string REVIEW = "review";
            public const string ACE = "ace";
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
            public const string ONACTIVATEEXTENSION = "on_activate_extension";
            public const string ONACTIVATEEXTENSIONERROR = "on_activate_extension_error";

            public const string OPENCODEHEALTHDOCS = "open_code_health_docs";
            public const string OPENDOCSPANEL = "open_interactive_docs_panel";

            public const string SETTINGSVISIBILITY = "control_center/visibility";
            public const string OPENSETTINGS = "control_center/open-settings";
            public const string OPENLINK = "control_center/open-link";

            public const string MONITORVISIBILITY = "code_health_monitor/visibility";
            public const string MONITORFILEADDED = "code_health_monitor/file_added";
            public const string MONITORFILEUPDATED = "code_health_monitor/file_updated";
            public const string MONITORFILEREMOVED = "code_health_monitor/file_removed";

            public const string DETAILSVISIBILITY = "code_health_details/visibility";
            public const string DETAILSFUNCTIONSELECTED = "code_health_details/function_selected";
            public const string DETAILSFUNCTIONDESELECTED = "code_health_details/function_deselected";

            public const string REVIEWORDELTATIMEOUT = "review_or_delta_timeout";
            public const string UNHANDLEDERROR = "unhandledError";
            public const string ANALYSISPERFORMANCE = "analysis/performance";

            public const string ACEINFOPRESENTED = "ace_info/presented";
            public const string ACEINFOACKNOWLEDGED = "ace_info/acknowledged";
            public const string ACEREFACTORREQUESTED = "refactor/requested";
            public const string ACEREFACTORPRESENTED = "refactor/presented";
            public const string ACEREFACTORAPPLIED = "refactor/applied";
            public const string ACEREFACTORREJECTED = "refactor/rejected";
            public const string ACEREFACTORCOPYCODE = "refactor/copy-code";
            public const string ACEREFACTORDIFFSHOWN = "refactor/diff_shown";

            public const string TERMSANDPOLICIESSHOWN = "terms_and_policies_shown";
            public const string TERMSANDPOLICIESRESPONSE = "terms_and_policies_response";
            public const string REVOKETERMS = "revoke_terms";
        }
    }
}
