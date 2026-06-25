// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Git
{
    internal static class DetectedFilePrioritySorter
    {
        public static IEnumerable<string> SortByVisibility(
            IEnumerable<string> filePaths,
            IEnumerable<string> visibleFileNames,
            string activeDocumentPath = null)
        {
            var visible = new HashSet<string>(visibleFileNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var visiblePaths = new List<string>();
            var hiddenPaths = new List<string>();

            foreach (var filePath in filePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (visible.Contains(filePath))
                {
                    visiblePaths.Add(filePath);
                }
                else
                {
                    hiddenPaths.Add(filePath);
                }
            }

            visiblePaths.Sort(StringComparer.OrdinalIgnoreCase);
            hiddenPaths.Sort(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(activeDocumentPath))
            {
                var activeIndex = visiblePaths.FindIndex(path => string.Equals(path, activeDocumentPath, StringComparison.OrdinalIgnoreCase));
                if (activeIndex > 0)
                {
                    var activePath = visiblePaths[activeIndex];
                    visiblePaths.RemoveAt(activeIndex);
                    visiblePaths.Insert(0, activePath);
                }
            }

            return visiblePaths.Concat(hiddenPaths);
        }
    }
}
