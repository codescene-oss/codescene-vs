using Newtonsoft.Json;

namespace CodesceneReeinventTest.Application.Services.FileReviewer.ReviewResult;

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
