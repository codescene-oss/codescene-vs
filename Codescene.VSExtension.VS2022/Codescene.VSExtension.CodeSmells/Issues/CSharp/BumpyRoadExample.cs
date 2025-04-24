using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Codescene.VSExtension.CodeSmells.Issues.CSharp
{
    class BumpyRoadExample
    {
        public void ProcessDirectory(string path)
        {
            var files = new List<string>();
            var directory = new DirectoryInfo(path);

            foreach (FileInfo fileInfo in directory.GetFiles())
            {
                if (Regex.IsMatch(fileInfo.Name, @"^data\d+\.csv$"))
                {
                    files.Add(fileInfo.FullName);
                }
            }

            var sb = new StringBuilder();
            foreach (string filePath in files)
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        sb.Append(line);
                    }
                }
            }

            using (var writer = new StreamWriter("data.csv"))
            {
                writer.Write(sb.ToString());
            }
        }
    }
}
