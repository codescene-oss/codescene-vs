using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;

namespace Codescene.VSExtension.CodeLensProvider.Providers.Base
{
    public class Constants
    {
        public const string DATA_POINT_PROVIDER_CONTENT_TYPE = "csharp";
        public class Titles
        {
            public const string CODESCENE_ACE = "Codescene ACE";
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

            public const string CODE_HEALTH_MONITOR = "Code Health Monitor";
            public const string GENERAL_CODE_HEALTH = "General Code Health";
        }
        public class Images
        {
            public static readonly ImageId WarningImageId = new ImageId(KnownMonikers.StatusWarningNoColor.Guid, KnownMonikers.StatusWarningNoColor.Id);
            public static readonly ImageId HeartbeatImageId = new ImageId(KnownMonikers.Wizard.Guid, KnownMonikers.Wizard.Id);
        }

    }
}
