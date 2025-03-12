using System;

namespace Codescene.VSExtension.DocumentationFetcher
{
    internal class Fetcher
    {
        public FetchResult Fetch()
        {
            try
            {
                //var token = Environment.GetEnvironmentVariable("CI") == "true"
                //    ? Environment.GetEnvironmentVariable("CODESCENE_IDE_DOCS_TOKEN")
                //    : Environment.GetEnvironmentVariable("GH_PACKAGE_TOKEN");

                //if (string.IsNullOrWhiteSpace(token))
                //    return new FetchResult { Success = false, Message = "Token is not provided!" };

                //var apiUrl = "https://api.github.com/repos/empear-analytics/codescene-ide-protocol/releases";
                //var releasesJson = GetJson(apiUrl, token);
                //var (tag, assetUrl) = parseResponse(releasesJson);
                //saveDocs(tag, assetUrl, token);

                return new FetchResult { Success = true };
            }
            catch (Exception ex)
            {
                return new FetchResult { Success = false, Message = ex.Message };
            }
        }

        /*private string GetJson(string url, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "DotNetApp");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
                return client.GetStringAsync(url).Result;
            }
        }

        private (string, string) parseResponse(string json)
        {
            var releases = JArray.Parse(json);
            var release = releases
                .FirstOrDefault(x => x["prerelease"]?.Value<bool>() == false
                                  && x["draft"]?.Value<bool>() == false) as JObject;
            if (release == null) return ("", "");

            var tag = release["tag_name"]?.ToString();
            if (string.IsNullOrWhiteSpace(tag)) return ("", "");

            var assets = release["assets"] as JArray;
            if (assets == null || assets.Count == 0) return ("", "");

            var docsAsset = assets
                .FirstOrDefault(x => x["name"]?.ToString() == "docs.zip") as JObject;
            if (docsAsset == null) return ("", "");

            var assetUrl = docsAsset["url"]?.ToString();
            return (tag, assetUrl);
        }

        private void saveDocs(string tag, string assetUrl, string token)
        {
            var resources = Path.Combine("src", "main", "resources");
            var docsFolder = Path.Combine(resources, "docs");
            var zipFilePath = Path.Combine(docsFolder, tag + ".zip");

            if (Directory.Exists(docsFolder)) Directory.Delete(docsFolder, true);
            Directory.CreateDirectory(docsFolder);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "DotNetApp");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                var data = client.GetByteArrayAsync(assetUrl).Result;
                File.WriteAllBytes(zipFilePath, data);
            }

            unzip(zipFilePath, resources);
        }

        private void unzip(string zipFilePath, string outputDir)
        {
            ZipFile.ExtractToDirectory(zipFilePath, outputDir);
            if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
        }*/
    }
}
