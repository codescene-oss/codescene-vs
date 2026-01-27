using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class TrackerManager
    {
        private readonly HashSet<string> _tracker = new HashSet<string>();
        private readonly object _lock = new object();

        public void Add(string filePath)
        {
            lock (_lock)
            {
                _tracker.Add(filePath);
            }
        }

        public bool Contains(string filePath)
        {
            lock (_lock)
            {
                return _tracker.Contains(filePath);
            }
        }

        public bool Remove(string filePath)
        {
            lock (_lock)
            {
                return _tracker.Remove(filePath);
            }
        }

        public List<string> GetFilesStartingWith(string prefix)
        {
            lock (_lock)
            {
                return _tracker.Where(tf => tf.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        public void RemoveAll(List<string> filesToRemove)
        {
            lock (_lock)
            {
                foreach (var file in filesToRemove)
                {
                    _tracker.Remove(file);
                }
            }
        }
    }
}
