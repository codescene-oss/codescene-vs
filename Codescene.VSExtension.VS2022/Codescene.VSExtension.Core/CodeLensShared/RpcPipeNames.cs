namespace Codescene.VSExtension.Core.CodeLensShared
{
    public static class RpcPipeNames
    {
        // Pipe needs to be scoped by PID so multiple VS instances don't compete for connecting CodeLenses.
        public static string ForCodeLens(int pid) => $@"CodeLensSample\vs\{pid}";
    }
}
