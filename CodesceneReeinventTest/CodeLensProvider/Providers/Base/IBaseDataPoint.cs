using Microsoft.VisualStudio.Language.CodeLens.Remoting;

namespace CodeLensProvider.Providers.Base
{
    public interface IBaseDataPoint : IAsyncCodeLensDataPoint
    {
        void Refresh();
        string DataPointId { get; }
        VisualStudioConnection VsConnection { get; set; }
    }
}
