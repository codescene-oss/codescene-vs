using System.Collections.Generic;
using Codescene.VSExtension.Core.Application.Services.Util;

namespace Codescene.VSExtension.VS2022.Util;

public static class DocumentationMappings
{
    public static readonly IReadOnlyDictionary<string, string> DocNameMap =
        new Dictionary<string, string>
        {
            { "docs_general_code_health", Constants.Titles.GENERAL_CODE_HEALTH },
            { "docs_code_health_monitor", Constants.Titles.CODE_HEALTH_MONITOR },
            { "docs_issues_brain_class", Constants.Titles.BRAIN_CLASS },
            { "docs_issues_brain_method", Constants.Titles.BRAIN_METHOD },
            { "docs_issues_bumpy_road_ahead", Constants.Titles.BUMPY_ROAD_AHEAD },
            { "docs_issues_complex_conditional", Constants.Titles.COMPLEX_CONDITIONAL },
            { "docs_issues_complex_method", Constants.Titles.COMPLEX_METHOD },
            { "docs_issues_constructor_over_injection", Constants.Titles.CONSTRUCTOR_OVER_INJECTION },
            { "docs_issues_duplicated_assertion_blocks", Constants.Titles.DUPLICATED_ASSERTION_BLOCKS },
            { "docs_issues_code_duplication", Constants.Titles.CODE_DUPLICATION },
            { "docs_issues_file_size_issue", Constants.Titles.FILE_SIZE_ISSUE },
            { "docs_issues_excess_number_of_function_arguments", Constants.Titles.EXCESS_NUMBER_OF_FUNCTION_ARGUMENTS },
            { "docs_issues_number_of_functions_in_a_single_module", Constants.Titles.NUMBER_OF_FUNCTIONS_IN_A_SINGLE_MODULE },
            { "docs_issues_global_conditionals", Constants.Titles.GLOBAl_CONDITIONALS },
            { "docs_issues_deep_global_nested_complexity", Constants.Titles.DEEP_GLOBAL_NESTED_COMPLEXITY },
            { "docs_issues_high_degree_of_code_duplication", Constants.Titles.HIGH_DEGREE_OF_CODE_DUPLICATION },
            { "docs_issues_large_assertion_blocks", Constants.Titles.LARGE_ASSERTION_BLOCKS },
            { "docs_issues_large_embedded_code_block", Constants.Titles.LARGE_EMBEDDED_CODE_BLOCK },
            { "docs_issues_large_method", Constants.Titles.LARGE_METHOD },
            { "docs_issues_lines_of_code_in_a_single_file", Constants.Titles.LINES_OF_CODE_IN_A_SINGLE_FILE },
            { "docs_issues_lines_of_declarations_in_a_single_file", Constants.Titles.LINES_OF_DECLARATION_IN_A_SINGLE_FILE },
            { "docs_issues_low_cohesion", Constants.Titles.LOW_COHESION },
            { "docs_issues_missing_arguments_abstractions", Constants.Titles.MISSING_ARGUMENTS_ABSTRACTIONS },
            { "docs_issues_modularity_issue", Constants.Titles.MODULARITY_ISSUE },
            { "docs_issues_deep_nested_complexity", Constants.Titles.DEEP_NESTED_COMPLEXITY },
            { "docs_issues_overall_code_complexity", Constants.Titles.OVERALL_CODE_COMPLEXITY },
            { "docs_issues_potentially_low_cohesion", Constants.Titles.POTENTIALLY_LOW_COHESION },
            { "docs_issues_primitive_obsession", Constants.Titles.PRIMITIVE_OBSESSION },
            { "docs_issues_string_heavy_function_arguments", Constants.Titles.STRING_HEAVY_FUNCTION_ARGUMENTS },
        };

}
