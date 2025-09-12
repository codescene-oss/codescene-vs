using System;

namespace Codescene.VSExtension.Core.Application.Services.Util
{
    public class Constants
    {
        public class Timeout
        {
            public static readonly TimeSpan DEFAULT_CLI_TIMEOUT = TimeSpan.FromMilliseconds(60000); // 60s
            public static readonly TimeSpan TELEMETRY_TIMEOUT = TimeSpan.FromMilliseconds(5000); // 5s
        }

        public class Titles
        {
            public const string CODESCENE = "CodeScene";

            #region Code Smell Names
            public const string BRAIN_CLASS = "Brain Class";
            public const string BRAIN_METHOD = "Brain Method";
            public const string BUMPY_ROAD_AHEAD = "Bumpy Road Ahead";
            public const string CODE_DUPLICATION = "Code Duplication";
            public const string COMPLEX_CONDITIONAL = "Complex Conditional";
            public const string COMPLEX_METHOD = "Complex Method";
            public const string CONSTRUCTOR_OVER_INJECTION = "Constructor Over Injection";
            public const string DEEP_GLOBAL_NESTED_COMPLEXITY = "Deep Global Nested Complexity";
            public const string DEEP_NESTED_COMPLEXITY = "Deep, Nested Complexity";
            public const string DUPLICATED_ASSERTION_BLOCKS = "Duplicated Assertion Blocks";
            public const string DUPLICATED_FUNCTION_BLOCKS = "Duplicated Function Blocks";
            public const string EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS = "Excess Number of Function Arguments";
            public const string FILE_SIZE_ISSUE = "File Size Issue";
            public const string GLOBAl_CONDITIONALS = "Global Conditionals";
            public const string HIGH_DEGREE_OF_CODE_DUPLICATION = "High Degree Of Code Duplication";
            public const string LARGE_ASSERTION_BLOCKS = "Large Assertion Blocks";
            public const string LARGE_EMBEDDED_CODE_BLOCK = "Large Embedded Code Block";
            public const string LARGE_METHOD = "Large Method";
            public const string LINES_OF_CODE_IN_A_SINGLE_FILE = "Lines Of Code In A Single File";
            public const string LINES_OF_DECLARATIONS_IN_A_SINGLE_FILE = "Lines Of Declarations In A Single File";
            public const string LOW_COHESION = "Low Cohesion";
            public const string MISSING_ARGUMENTS_ABSTRACTIONS = "Missing Arguments Abstractions";
            public const string MODULARITY_ISSUE = "Modularity Issue";
            public const string NUMBER_OF_FUNCTIONS_IN_A_SINGLE_MODULE = "Number Of Functions In A Single Module";
            public const string OVERALL_CODE_COMPLEXITY = "Overall Code Complexity";
            public const string POTENTIALLY_LOW_COHESION = "Potentially Low Cohesion";
            public const string PRIMITIVE_OBSESSION = "Primitive Obsession";
            public const string STRING_HEAVY_FUNCTION_ARGUMENTS = "String Heavy Function Arguments";
            #endregion

            public const string CODE_HEALTH_MONITOR = "Code Health Monitor";
            public const string GENERAL_CODE_HEALTH = "General Code Health";
            public const string COMMIT_BASELINE = "CommitBaseline";

            #region Terms & Policies
            public const string ACCEPT_TERMS = "Accept";
            public const string DECLINE_TERMS = "Decline";
            public const string VIEW_TERMS = "View Terms & Policies";
            public const string TERMS_INFO = "By using this extension you agree to CodeScene's Terms and Privacy Policy";
            public const string SETTINGS_COLLECTION = "CodeSceneExtension";
            public const string ACCEPTED_TERMS_PROPERTY = "AcceptedTerms";
            #endregion
        }

        public class Telemetry
        {
            public const string SOURCE_IDE = "vs";

            #region Events
            public const string ON_ACTIVATE_EXTENSION = "on_activate_extension";
            public const string ON_ACTIVATE_EXTENSION_ERROR = "on_activate_extension_error";

            public const string OPEN_CODE_HEALTH_DOCS = "open_code_health_docs";
            public const string OPEN_DOCS_PANEL = "open_interactive_docs_panel";

            public const string SETTINGS_VISIBILITY = "control_center/visibility";
            public const string OPEN_SETTINGS = "control_center/open-settings";
            public const string OPEN_LINK = "control_center/open-link";

            public const string MONITOR_VISIBILITY = "code_health_monitor/visibility";
            public const string MONITOR_FILE_ADDED = "code_health_monitor/file_added";
            public const string MONITOR_FILE_UPDATED = "code_health_monitor/file_updated";
            public const string MONITOR_FILE_REMOVED = "code_health_monitor/file_removed";

            public const string DETAILS_VISIBILITY = "code_health_details/visibility";
            public const string DETAILS_FUNCTION_SELECTED = "code_health_details/function_selected";
            public const string DETAILS_FUNCTION_DESELECTED = "code_health_details/function_deselected";

            public const string REVIEW_OR_DELTA_TIMEOUT = "review_or_delta_timeout";

            public const string ACE_INFO_PRESENTED = "ace_info/presented";
            public const string ACE_INFO_ACKNOWLEDGED = "ace_info/acknowledged";
            public const string ACE_REFACTOR_REQUESTED = "refactor/requested";
            public const string ACE_REFACTOR_PRESENTED = "refactor/presented";
            public const string ACE_REFACTOR_APPLIED = "refactor/applied";
            public const string ACE_REFACTOR_REJECTED = "refactor/rejected";
            public const string ACE_REFACTOR_COPY_CODE = "refactor/copy-code";
            public const string ACE_REFACTOR_DIFF_SHOWN = "refactor/diff_shown";

            public const string TERMS_AND_POLICIES_SHOWN = "terms_and_policies_shown";
            public const string TERMS_AND_POLICIES_RESPONSE = "terms_and_policies_response";
            public const string REVOKE_TERMS = "revoke_terms";

            public const string COMMIT_BASELINE_CHANGED = "commit_baseline_changed";
            #endregion
        }
    }
}