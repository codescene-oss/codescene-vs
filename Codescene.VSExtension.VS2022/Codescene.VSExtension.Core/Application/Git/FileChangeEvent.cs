namespace Codescene.VSExtension.Core.Application.Git
{
    public enum FileChangeType
    {
        Create,
        Change,
        Delete
    }

    public class FileChangeEvent
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
