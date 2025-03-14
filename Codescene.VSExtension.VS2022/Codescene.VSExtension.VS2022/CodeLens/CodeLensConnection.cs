using StreamJsonRpc;
using System.IO.Pipes;

namespace Codescene.VSExtension.VS2022.CodeLens
{
    internal class CodeLensConnection
    {
        public JsonRpc Rpc;
        private readonly NamedPipeServerStream _stream;

        public CodeLensConnection(NamedPipeServerStream stream)
        {
            _stream = stream;
            Rpc = JsonRpc.Attach(_stream, this);
        }
    }
}
