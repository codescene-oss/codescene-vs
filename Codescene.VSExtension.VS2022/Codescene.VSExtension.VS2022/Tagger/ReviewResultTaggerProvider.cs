using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.UnderlineTagger;

[Export(typeof(ITaggerProvider))]
[ContentType("code")]
[TagType(typeof(IErrorTag))]
public class ReviewResultTaggerProvider : ITaggerProvider
{
    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    /*
     * CreateTagger is called when a tagger is requested for a buffer.
     * This typically happens the first time a file is opened in the editor,
       or when the file is closed and opened again.
     * It does NOT get called simply when switching focus between already opened files.
     */
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        var path = buffer?.GetFileName();
        if (buffer == null)
        {
            _logger.Warn($"Could not create tagger for undefined buffer.");
            return null;
        }

        var isSupportedForReview = _supportedFileChecker.IsSupported(path);
        if (!isSupportedForReview)
        {
            _logger.Debug($"File {path} is not supported for review. Skipping tagger creation...");
            return null;
        }

        return buffer
            .Properties
            .GetOrCreateSingletonProperty(() => // avoid duplicate taggers for the same buffer
            new ReviewResultTagger(buffer, path)) as ITagger<T>;
    }
}
