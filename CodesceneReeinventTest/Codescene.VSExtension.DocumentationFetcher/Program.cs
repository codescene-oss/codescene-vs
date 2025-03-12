namespace Codescene.VSExtension.DocumentationFetcher
{
    class Program
    {
        static int Main(string[] args)
        {
            var fetcher = new Fetcher();
            var result = fetcher.Fetch();

            if (result.Success)
                return 0;

            return 1;
        }
    }
}
