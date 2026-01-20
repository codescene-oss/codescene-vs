namespace Codescene.VSExtension.VS2022.Application.Git
{
    internal enum FileChangeType
    {
        Create,
        Change,
        Delete
    }

    internal class FileChangeEvent
    {
        public FileChangeType Type { get; }
        public string FilePath { get; }

        public FileChangeEvent(FileChangeType type, string filePath)
        {
            Type = type;
            FilePath = filePath;
        }
    }
}
