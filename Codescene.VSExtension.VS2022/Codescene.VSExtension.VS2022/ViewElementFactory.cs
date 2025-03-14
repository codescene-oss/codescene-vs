using Codescene.VSExtension.CodeLensShared;
using Codescene.VSExtension.VS2022.Controls;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;

namespace Codescene.VSExtension.VS2022
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

            if (model is CustomDetailsData detailsData)
            {
                if (detailsData.FileName == "brain-class")
                {
                    var detailsUI = new BrainClass();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "brain-method")
                {
                    var detailsUI = new BrainMethod();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "bumpy-road-ahead")
                {
                    var detailsUI = new BumpyRoadAhead();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "code-duplication")
                {
                    var detailsUI = new CodeDuplication();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "complex-conditional")
                {
                    var detailsUI = new ComplexConditional();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "complex-method")
                {
                    var detailsUI = new ComplexMethod();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "constructor-over-injection")
                {
                    var detailsUI = new ConstructorOverInjection();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "deep-global-nested-complexity")
                {
                    var detailsUI = new DeepGlobalNestedComplexity();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "deep-nested-complexity")
                {
                    var detailsUI = new DeepNestedComplexity();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "duplicated-assertion-blocks")
                {
                    var detailsUI = new DuplicatedAssertionBlocks();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "duplicated-function-blocks")
                {
                    var detailsUI = new DuplicatedFunctionBlocks();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "excess-number-of-function-arguments")
                {
                    var detailsUI = new ExcessNumberOfFunctionArguments();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "file-size-issue")
                {
                    var detailsUI = new FileSizeIssue();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "global-conditionals")
                {
                    var detailsUI = new GlobalConditionals();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "high-degree-of-code-duplication")
                {
                    var detailsUI = new HighDegreeOfCodeDuplication();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "large-assertion-blocks")
                {
                    var detailsUI = new LargeAssertionBlocks();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "large-embedded-code-block")
                {
                    var detailsUI = new LargeEmbeddedCodeBlock();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "large-method")
                {
                    var detailsUI = new LargeMethod();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "lines-of-code-in-a-single-file")
                {
                    var detailsUI = new LinesOfCodeInASingleFile();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "lines-of-declarations-in-a-single-file")
                {
                    var detailsUI = new LinesOfDeclarationsInASingleFile();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "low-cohesion")
                {
                    var detailsUI = new LowCohesion();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "missing-arguments-abstractions")
                {
                    var detailsUI = new MissingArgumentsAbstractions();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "modularity-issue")
                {
                    var detailsUI = new ModularityIssue();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "number-of-functions-in-a-single-module")
                {
                    var detailsUI = new NumberOfFunctionsInASingleModule();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "overall-code-complexity")
                {
                    var detailsUI = new OverallCodeComplexity();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "potentially-low-cohesion")
                {
                    var detailsUI = new PotentiallyLowCohesion();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "primitive-obsession")
                {
                    var detailsUI = new PrimitiveObsession();
                    return detailsUI as TView;
                }
                else if (detailsData.FileName == "string-heavy-function-arguments")
                {
                    var detailsUI = new StringHeavyFunctionArguments();
                    return detailsUI as TView;
                }
                if (detailsData.FileName == "general-code-health")
                {
                    var detailsUI = new GeneralCodeHealth();
                    return detailsUI as TView;
                }
                else
                {
                    var detailsUI = new CodeHealthMonitor();
                    return detailsUI as TView;
                }
                //detailsUI.DataContext = detailsData;
            }

            return null;
        }
    }
}
