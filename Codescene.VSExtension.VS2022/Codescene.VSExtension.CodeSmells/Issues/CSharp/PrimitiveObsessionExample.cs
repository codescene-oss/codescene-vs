namespace Codescene.VSExtension.CodeSmells.Issues.CSharp
{
    interface IHttpClient
    {
        bool Get(string url);
    }

    class PrimitiveObsessionExample
    {
        private readonly IHttpClient httpClient;
        private readonly string _baseUrl = string.Empty;
        public bool Search(string query, int? pages, int? pageSize)
        {
            return httpClient.Get(string.Format("{0}?q={1}&pages={2}&pageSize={3}",
                                    _baseUrl,
                                    query,
                                    pages ?? 10,
                                    pageSize ?? 10));
        }
    }
}
