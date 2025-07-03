using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Models.ReviewModels;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    [Export(typeof(ICodeReviewer))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeReviewer : ICodeReviewer
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IModelMapper _mapper;

        [Import]
        private readonly ICliExecutor _executer;

        [Import]
        private readonly ITelemetryManager _telemetryManager;

        public FileReviewModel Review(string path, string content)
        {
            var fileName = Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(content))
            {
                _logger.Warn($"Could not review path {path}. Missing content or file path.");
                return null;
            }

            var review = _executer.ReviewContent(fileName, content);
            _telemetryManager.SendTelemetryAsync("test-event");
            return _mapper.Map(path, review); ;
        }
    }
}
