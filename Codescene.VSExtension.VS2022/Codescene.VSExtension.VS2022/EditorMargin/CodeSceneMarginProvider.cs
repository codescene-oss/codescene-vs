using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    private readonly IReviewedFilesCacheHandler _cache;

    [Import]
    private readonly OnDocumentSavedHandler _activeDocumentTextChangeHandler;

    public IWpfTextViewMargin CreateMargin(
        IWpfTextViewHost textViewHost,
        IWpfTextViewMargin marginContainer)
    {
        return new CodeSceneMargin(_activeDocumentTextChangeHandler, _cache) as IWpfTextViewMargin;
    }
}

