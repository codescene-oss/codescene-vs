using Microsoft.VisualStudio.Language.CodeLens;
using Microsoft.VisualStudio.Language.CodeLens.Remoting;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Abstraction
{
    public interface ICodesceneCodelensCallbackService
    {
        float GetFileReviewScore(string filePath);
        bool ShowCodeLensForFunction(string issue, string filePath, int startLine);
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
        bool IsCodeSceneLensesEnabled();
        Task OpenAceToolWindowAsync(CodeLensDescriptor descriptor, CodeLensDescriptorContext context);
        /// <summary>
        /// For Debug Purpose
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        bool SendError(string ex);
    }
}
