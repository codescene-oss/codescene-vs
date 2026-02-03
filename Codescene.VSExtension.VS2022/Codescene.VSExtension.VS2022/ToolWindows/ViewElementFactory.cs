using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.Controls;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Windows;

namespace Codescene.VSExtension.VS2022.CodeLens
{
    [Export(typeof(IViewElementFactory))]
    [Name("Git commit details UI factory")]
    [TypeConversion(from: typeof(CustomDetailsData), to: typeof(FrameworkElement))]
    [Order]
    internal class ViewElementFactory : IViewElementFactory
    {
        public TView CreateViewElement<TView>(ITextView textView, object model) where TView : class
        {
            if (typeof(FrameworkElement) != typeof(TView))
            {
                throw new ArgumentException($"Invalid type conversion. Unsupported {nameof(model)} or {nameof(TView)} type");
            }

            if (model is CustomDetailsData { FileName: var fileName })
            {
                // Svi naši UI‑kontroli nasljeđuju FrameworkElement,
                // a ranije smo već provjerili da je TView == FrameworkElement.
                FrameworkElement view = fileName switch
                {
                    "brain-class" => new BrainClass(),
                    "brain-method" => new BrainMethod(),
                    "bumpy-road-ahead" => new BumpyRoadAhead(),
                    "code-duplication" => new CodeDuplication(),
                    "complex-conditional" => new ComplexConditional(),
                    "complex-method" => new ComplexMethod(),
                    "constructor-over-injection" => new ConstructorOverInjection(),
                    "deep-global-nested-complexity" => new DeepGlobalNestedComplexity(),
                    "deep-nested-complexity" => new DeepNestedComplexity(),
                    "duplicated-assertion-blocks" => new DuplicatedAssertionBlocks(),
                    "duplicated-function-blocks" => new DuplicatedFunctionBlocks(),
                    "excess-number-of-function-arguments" => new ExcessNumberOfFunctionArguments(),
                    "file-size-issue" => new FileSizeIssue(),
                    "global-conditionals" => new GlobalConditionals(),
                    "high-degree-of-code-duplication" => new HighDegreeOfCodeDuplication(),
                    "large-assertion-blocks" => new LargeAssertionBlocks(),
                    "large-embedded-code-block" => new LargeEmbeddedCodeBlock(),
                    "large-method" => new LargeMethod(),
                    "lines-of-code-in-a-single-file" => new LinesOfCodeInASingleFile(),
                    "lines-of-declarations-in-a-single-file" => new LinesOfDeclarationsInASingleFile(),
                    "low-cohesion" => new LowCohesion(),
                    "missing-arguments-abstractions" => new MissingArgumentsAbstractions(),
                    "modularity-issue" => new ModularityIssue(),
                    "number-of-functions-in-a-single-module" => new NumberOfFunctionsInASingleModule(),
                    "overall-code-complexity" => new OverallCodeComplexity(),
                    "potentially-low-cohesion" => new PotentiallyLowCohesion(),
                    "primitive-obsession" => new PrimitiveObsession(),
                    "string-heavy-function-arguments" => new StringHeavyFunctionArguments(),
                    "general-code-health" => new GeneralCodeHealth(),
                    _ => new CodeHealthMonitor()   // default
                };

                return view as TView;
            }

            return null;
        }
    }
}
