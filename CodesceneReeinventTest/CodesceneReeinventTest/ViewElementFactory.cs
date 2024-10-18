using CodeLensShared;
using CodesceneReeinventTest.Controls;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows;

namespace CodesceneReeinventTest
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
                if (detailsData.FileName == "general-code-health")
                {
                    var detailsUI = new GeneralCodeHealth();
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
                else if (detailsData.FileName == "bumpy-road-ahead")
                {
                    var detailsUI = new BumpyRoadAhead();
                    return detailsUI as TView;
                }
                else
                {
                    var detailsUI = new ExcessNumberOfFunctionArguments(); ;
                    return detailsUI as TView;
                }
                //detailsUI.DataContext = detailsData;
            }

            return null;
        }
    }
}
