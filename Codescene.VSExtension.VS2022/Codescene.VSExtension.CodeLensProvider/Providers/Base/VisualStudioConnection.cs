using Codescene.VSExtension.CodeLensProvider.Abstraction;
using StreamJsonRpc;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CodeLensProvider.Providers.Base
{
    public class VisualStudioConnection : IRemoteCodeLens
    {
        private readonly NamedPipeClientStream _stream;
        private readonly IBaseDataPoint _owner;
        public JsonRpc Rpc;

        public VisualStudioConnection(IBaseDataPoint owner, int vsPid)
        {
            _owner = owner;
            _stream = new NamedPipeClientStream(
                serverName: ".",
                RpcPipeNames.ForCodeLens(vsPid),
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            );
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _stream.ConnectAsync(cancellationToken).ConfigureAwait(false);
                Rpc = JsonRpc.Attach(_stream, this);
            }
            catch (System.Exception ex)
            {
                throw new System.Exception($"ConnectAsync exception:{ex.Message}");
            }
        }

        public void Refresh() => _owner.Refresh();
    }
}
