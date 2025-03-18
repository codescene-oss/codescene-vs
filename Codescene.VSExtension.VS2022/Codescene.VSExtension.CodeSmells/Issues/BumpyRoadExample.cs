using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Codescene.VSExtension.CodeSmells.Issues
{
    class BumpyRoadExample
    {
        public void ProcessDirectory(string path)
        {
            // Pronađi sve datoteke koje odgovaraju obrascu "data<number>.csv".
            var files = new List<string>();
            var directory = new DirectoryInfo(path);

            foreach (FileInfo fileInfo in directory.GetFiles())
            {
                // Provjeravamo uzorak: "data\\d+\\.csv"
                if (Regex.IsMatch(fileInfo.Name, @"^data\d+\.csv$")) 
                {
                    files.Add(fileInfo.FullName);
                }
            }

            // Konkateniraj sve pronađene datoteke u jedan string
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

            // Zapiši sve u novu datoteku "data.csv"
            using (var writer = new StreamWriter("data.csv"))
            {
                writer.Write(sb.ToString());
            }
        }
    }
}
