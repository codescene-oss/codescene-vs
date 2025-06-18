using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Utilities.Internal;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

[Export(typeof(ITaggerProvider))]
[ContentType("code")]
[TagType(typeof(IErrorTag))]
public class ReviewResultTaggerProvider : ITaggerProvider
{
    [Import]
    private readonly ILogger _logger;

    /*
     * CreateTagger is called when a tagger is requested for a buffer.
     * This typically happens the first time a file is opened in the editor,
       or when the file is closed and opened again.
     * It does NOT get called simply when switching focus between already opened files.
     */
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        var path = buffer?.GetFileName();
        if (buffer == null || path.IsNullOrWhiteSpace())
        {
            _logger.Warn($"Could not create tagger for undefined buffer.");
            return null;
        }

        _logger.Info($"Creating tagger for {buffer.GetFileName()}");
        return buffer
            .Properties
            .GetOrCreateSingletonProperty(() => // avoid duplicate taggers for the same buffer
            new ReviewResultTagger(buffer, path)) as ITagger<T>;
    }

}
