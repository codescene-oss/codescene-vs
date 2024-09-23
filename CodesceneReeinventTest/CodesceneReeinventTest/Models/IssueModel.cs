namespace CodesceneReeinventTest.Models;

public class IssueModel
{
    public string Resource { get; set; }
    public string Owner { get; set; }
    public CodeModel Code { get; set; }
    public int Severity { get; set; }
    public string Message { get; set; }
    public string Source { get; set; }
    public int StartLineNumber { get; set; }
    public int StartColumn { get; set; }
    public int EndLineNumber { get; set; }
    public int EndColumn { get; set; }
}
