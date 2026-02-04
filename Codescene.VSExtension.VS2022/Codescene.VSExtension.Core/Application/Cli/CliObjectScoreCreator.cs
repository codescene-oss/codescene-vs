// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Text;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliObjectScoreCreator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliObjectScoreCreator : ICliObjectScoreCreator
    {
        private readonly ILogger _logger;

        [ImportingConstructor]
        public CliObjectScoreCreator(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// <param name="oldScore">Raw base64 encoded old score.</param>
        /// <param name="newScore">Raw base64 encoded new score.</param>
        /// <returns>score as json</returns>
        /// </summary>
        public string Create(string oldScore, string newScore)
        {
            if (string.IsNullOrWhiteSpace(oldScore) && string.IsNullOrWhiteSpace(newScore))
            {
                return string.Empty;
            }

            // No need to run the delta command if the scores are the same
            if (oldScore == newScore)
            {
                _logger.Debug("Scores are the same, skipping delta...");
                return string.Empty;
            }

            var sb = new StringBuilder("{");

            var oldScoreExists = false;
            if (!string.IsNullOrWhiteSpace(oldScore))
            {
                sb.Append($"\"old-score\":\"{oldScore}\"");
                oldScoreExists = true;
            }

            if (!string.IsNullOrWhiteSpace(newScore))
            {
                if (oldScoreExists)
                {
                    sb.Append(",");
                }

                sb.Append($"\"new-score\":\"{newScore}\"");
            }

            sb.Append("}");

            return sb.ToString();
        }
    }
}
