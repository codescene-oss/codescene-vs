using Newtonsoft.Json;

namespace CodeLensShared
{
    public class Function
    {
        public string Title { get; set; }
        public string Details { get; set; }

        [JsonProperty("start-line")]
        public int Startline { get; set; }

        [JsonProperty("end-line")]
        public int Endline { get; set; }
        public string Url { get; set; }
    }
}
