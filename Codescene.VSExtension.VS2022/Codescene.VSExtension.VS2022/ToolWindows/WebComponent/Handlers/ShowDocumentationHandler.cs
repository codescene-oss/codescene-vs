using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(ShowDocumentationHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDocumentationHandler
{
    public async Task HandleAsync(string path)
    {
        if (CodeSmellDocumentationWindow.IsCreated())
        {
            SetViewToLoadingMode(path);
        }

        await CodeSmellDocumentationWindow.ShowAsync();
    }
    private void SetViewToLoadingMode(string path)
    {
        CodeSmellDocumentationWindow.UpdateView(new ShowDocsMessage
        {
            MessageType = MessageTypes.UPDATE_RENDERER,
            Payload = new ShowDocsPayload
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.DOCS,
                Data = new ShowDocsModel
                {
                    DocType = DocTypes.DOCS_IMPROVEMENT_GUIDES_BUMPY_ROAD_AHEAD,
                    AutoRefactor = new AutoRefactorModel
                    {
                        Activated = false,
                        Disabled = false,
                        Visible = true
                    },
                    FileData = new FileDataModel
                    {
                        FileName = "BumpyRoadAhead.cs",
                        Fn = new FunctionModel
                        {
                            Name = "extract_identifiers",
                            Range = new RangeModel
                            {
                                StartLine = 1,
                                StartColumn = 1,
                                EndLine = 1,
                                EndColumn = 1,
                            }
                        },
                        Action = new ActionModel
                        {
                            GoToFunctionLocationPayload = new GoToFunctionLocationPayloadModel
                            {
                                FileName = "BumpyRoadAhead.cs",
                                Fn = new FunctionModel
                                {
                                    Name = "extract_identifiers",
                                    Range = new RangeModel
                                    {
                                        StartLine = 1,
                                        StartColumn = 1,
                                        EndLine = 1,
                                        EndColumn = 1,
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

