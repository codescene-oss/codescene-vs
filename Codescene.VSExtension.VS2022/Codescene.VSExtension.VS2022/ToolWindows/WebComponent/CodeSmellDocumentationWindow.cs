using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSmellDocumentationWindow : BaseToolWindow<CodeSmellDocumentationWindow>
{
    private static WebComponentUserControl _userControl = null;
    private static ShowDocsPayload _pendingPayload;

    public override Type PaneType => typeof(Pane);

    //TODO: extract helper
    public static string ToSnakeCase(string input)
    {
        var cleaned = Regex.Replace(input, @"[^\w\s]", "");
        return string.Join("_",
            cleaned
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.ToLowerInvariant()));
    }

    public static void SetPendingPayload(ShowDocumentationParams showDocsParams)
    {
        //TODO: extract common logic
        _pendingPayload = new ShowDocsPayload
        {
            IdeType = VISUAL_STUDIO_IDE_TYPE,
            View = ViewTypes.DOCS,
            Data = new ShowDocsModel
            {
                DocType = $"docs_issues_{ToSnakeCase(showDocsParams.Category)}",
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
                        Name = showDocsParams.FunctionName,
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
                            Filename = Path.GetFileName(showDocsParams.Path),
                            Fn = new FunctionModel
                            {
                                Name = showDocsParams.FunctionName,
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
        };
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {

        var logger = await VS.GetMefServiceAsync<ILogger>();

        if (_pendingPayload != null)
        {
            var ctrl = new WebComponentUserControl(_pendingPayload, logger)
            {
                CloseRequested = async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await HideAsync();
                }
            };

            _userControl = ctrl;

            return ctrl;
        }

        return null;
    }

    public override string GetTitle(int toolWindowId) => "Code smell documentation";

    public static bool IsCreated() => _userControl != null;

    public static void UpdateView(ShowDocsMessage message)
    {
        _userControl.UpdateView(message);
    }

    [Guid("D9D9979D-0D9C-439A-9062-33945D63FAF8")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}

