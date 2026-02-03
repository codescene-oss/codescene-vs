using System.Collections.Generic;
using Codescene.VSExtension.Core.Consts;

namespace Codescene.VSExtension.VS2022.Util;

public static class DocumentationMappings
{
    public static readonly IReadOnlyDictionary<string, string> DocNameMap =
        new Dictionary<string, string>
        {
            { "docs_general_code_health", Constants.Titles.GENERALCODEHEALTH },
            { "docs_code_health_monitor", Constants.Titles.CODEHEALTHMONITOR },
            { "docs_issues_brain_class", Constants.Titles.BRAINCLASS },
            { "docs_issues_brain_method", Constants.Titles.BRAINMETHOD },
            { "docs_issues_bumpy_road_ahead", Constants.Titles.BUMPYROADAHEAD },
            { "docs_issues_complex_conditional", Constants.Titles.COMPLEXCONDITIONAL },
            { "docs_issues_complex_method", Constants.Titles.COMPLEXMETHOD },
            { "docs_issues_constructor_over_injection", Constants.Titles.CONSTRUCTOROVERINJECTION },
            { "docs_issues_duplicated_assertion_blocks", Constants.Titles.DUPLICATEDASSERTIONBLOCKS },
            { "docs_issues_code_duplication", Constants.Titles.CODEDUPLICATION },
            { "docs_issues_file_size_issue", Constants.Titles.FILESIZEISSUE },
            { "docs_issues_excess_number_of_function_arguments", Constants.Titles.EXCESSNUMBEROFFUNCTIONARGUMENTS },
            { "docs_issues_number_of_functions_in_a_single_module", Constants.Titles.NUMBEROFFUNCTIONSINASINGLEMODULE },
            { "docs_issues_global_conditionals", Constants.Titles.GLOBAlCONDITIONALS },
            { "docs_issues_deep_global_nested_complexity", Constants.Titles.DEEPGLOBALNESTEDCOMPLEXITY },
            { "docs_issues_high_degree_of_code_duplication", Constants.Titles.HIGHDEGREEOFCODEDUPLICATION },
            { "docs_issues_large_assertion_blocks", Constants.Titles.LARGEASSERTIONBLOCKS },
            { "docs_issues_large_embedded_code_block", Constants.Titles.LARGEEMBEDDEDCODEBLOCK },
            { "docs_issues_large_method", Constants.Titles.LARGEMETHOD },
            { "docs_issues_lines_of_code_in_a_single_file", Constants.Titles.LINESOFCODEINASINGLEFILE },
            { "docs_issues_lines_of_declarations_in_a_single_file", Constants.Titles.LINESOFDECLARATIONSINASINGLEFILE },
            { "docs_issues_low_cohesion", Constants.Titles.LOWCOHESION },
            { "docs_issues_missing_arguments_abstractions", Constants.Titles.MISSINGARGUMENTSABSTRACTIONS },
            { "docs_issues_modularity_issue", Constants.Titles.MODULARITYISSUE },
            { "docs_issues_deep_nested_complexity", Constants.Titles.DEEPNESTEDCOMPLEXITY },
            { "docs_issues_overall_code_complexity", Constants.Titles.OVERALLCODECOMPLEXITY },
            { "docs_issues_potentially_low_cohesion", Constants.Titles.POTENTIALLYLOWCOHESION },
            { "docs_issues_primitive_obsession", Constants.Titles.PRIMITIVEOBSESSION },
            { "docs_issues_string_heavy_function_arguments", Constants.Titles.STRINGHEAVYFUNCTIONARGUMENTS },
        };
}
