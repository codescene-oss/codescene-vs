using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;
using static Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers.ShowDocumentationParams;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

public class ShowDocumentationParams(string path, string category, string functionName, CodeSmellRange range)
{
    public string Path { get; set; } = path;
    public string Category { get; set; } = category;
    public string FunctionName { get; set; } = functionName;
    public CodeSmellRange Range { get; set; } = range;

    public class CodeSmellRange(int startLine, int endLine, int startColumn, int endColumn)
    {
        public int StartLine { get; set; } = startLine;
        public int EndLine { get; set; } = endLine;
        public int StartColumn { get; set; } = startColumn;
        public int EndColumn { get; set; } = endColumn;

    }
}

[Export(typeof(ShowDocumentationHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDocumentationHandler
{
    public async Task HandleAsync(ShowDocumentationParams showDocsParams)
    {
        if (CodeSmellDocumentationWindow.IsCreated())
        {
            SetViewToLoadingMode(showDocsParams);
        }

        CodeSmellDocumentationWindow.SetPendingPayload(showDocsParams);

        await CodeSmellDocumentationWindow.ShowAsync();
    }

    private void SetViewToLoadingMode(ShowDocumentationParams showDocsParams)
    {
        //TODO: extract common logic
        CodeSmellDocumentationWindow.UpdateView(new ShowDocsMessage
        {
            MessageType = MessageTypes.UPDATE_RENDERER,
            Payload = new ShowDocsPayload
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.DOCS,
                Data = new ShowDocsModel
                {
                    DocType = $"docs_issues_{CodeSmellDocumentationWindow.ToSnakeCase(showDocsParams.Category)}",
                    AutoRefactor = new AutoRefactorModel
                    {
                        Activated = false,
                        Disabled = true,
                        Visible = false
                    },
                    FileData = new FileDataModel
                    {
                        Filename = Path.GetFileName(showDocsParams.Path),
                        Fn = new FunctionModel
                        {
                            Name = Path.GetFileName(showDocsParams.Path),
                            Range = new RangeModel
                            {
                                StartLine = showDocsParams.Range.StartLine,
                                StartColumn = showDocsParams.Range.StartColumn,
                                EndLine = showDocsParams.Range.EndLine,
                                EndColumn = showDocsParams.Range.EndColumn,
                            }
                        },
                        Action = new ActionModel
                        {
                            GoToFunctionLocationPayload = new GoToFunctionLocationPayloadModel
                            {
                                Filename = Path.GetFileName(showDocsParams.Path),
                                Fn = new FunctionModel
                                {
                                    Name = showDocsParams.FunctionName,
                                    Range = new RangeModel
                                    {
                                        StartLine = showDocsParams.Range.StartLine,
                                        StartColumn = showDocsParams.Range.StartColumn,
                                        EndLine = showDocsParams.Range.EndLine,
                                        EndColumn = showDocsParams.Range.EndColumn,
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
    }

}
