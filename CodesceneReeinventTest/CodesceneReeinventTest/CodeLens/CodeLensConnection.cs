using StreamJsonRpc;
using System.IO.Pipes;

namespace CodesceneReeinventTest.CodeLens
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
