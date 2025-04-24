using System.ComponentModel.Composition;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(IObjectScoreCreator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ObjectScoreCreator : IObjectScoreCreator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldScore">Raw base64 encoded score</param>
        /// <param name="newScore">Raw base64 encoded score</param>
        /// <returns></returns>
        public string Create(string oldScore, string newScore)
        {
            if (string.IsNullOrWhiteSpace(oldScore) && string.IsNullOrWhiteSpace(newScore))
            {
                return string.Empty;
            }

            // No need to run the delta command if the scores are the same
            if (oldScore == newScore)
            {
                return string.Empty;
            }

            var sb = new StringBuilder("{");

            var oldScoreExists = false;
            if (!string.IsNullOrWhiteSpace(oldScore))
            {
                sb.Append($" 'old-score':{oldScore} ");
                oldScoreExists = true;
            }

            if (!string.IsNullOrWhiteSpace(newScore))
            {
                if (oldScoreExists)
                {
                    sb.Append(", ");
                }

                sb.Append($" 'new-score':{newScore} ");
            }

            sb.Append("}");


            return sb.ToString();
        }
    }
}
