using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.EditorMargin;

[Export(typeof(IWpfTextViewMarginProvider))]
[MarginContainer(PredefinedMarginNames.Bottom)]
[Name("CodeSceneMargin")]
[ContentType("text")]
[TextViewRole(PredefinedTextViewRoles.Document)]
[PartCreationPolicy(CreationPolicy.Shared)]
public class CodeSceneMarginProvider : IWpfTextViewMarginProvider
{
    [Import]
    private readonly CodeSceneMarginSettingsManager _settings;

    public IWpfTextViewMargin CreateMargin(
        IWpfTextViewHost textViewHost,
        IWpfTextViewMargin marginContainer)
    {
        return new CodeSceneMargin(_settings) as IWpfTextViewMargin;
    }
}

