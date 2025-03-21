using System;

namespace Codescene.VSExtension.VS2022.ErrorList
{
    public class DocumentClosedEventArgs : EventArgs
    {
        /// <summary>
        /// Full file path to the document being closed
        /// </summary>
        public string FullPath { get; }

        public DocumentClosedEventArgs(string fullPath)
        {
            FullPath = fullPath;
        }
    }

    /// <summary>
    /// Raises notifications about document events
    /// </summary>
    public interface IDocumentEvents
    {
        /// <summary>
        /// Notification that a document has closed
        /// </summary>
        event EventHandler<DocumentClosedEventArgs> DocumentClosed;
    }
}
