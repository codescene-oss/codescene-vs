using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using CodeSceneConstants = Codescene.VSExtension.Core.Application.Services.Util.Constants;

namespace Codescene.VSExtension.VS2022.TermsAndPolicies;

public class InfoBarEvents : IVsInfoBarUIEvents
{
    private readonly Action<bool> _setAcceptedTerms;

    [Import]
    private readonly ILogger _logger;

    public InfoBarEvents(Action<bool> setAcceptedTerms) => _setAcceptedTerms = setAcceptedTerms;

    public void OnActionItemClicked(IVsInfoBarUIElement element, IVsInfoBarActionItem actionItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        SendTelemetry(CodeSceneConstants.Telemetry.TERMS_AND_POLICIES_RESPONSE, actionItem.Text);

        switch (actionItem.Text)
        {
            case CodeSceneConstants.Titles.AcceptTerms:
                _setAcceptedTerms(true);
                _logger.Info("User has accepted Terms & Conditions.");

                element.Close();
                break;
            case CodeSceneConstants.Titles.DeclineTerms:
                _setAcceptedTerms(false);
                _logger.Info("User has declined Terms & Conditions.");

                element.Close();
                break;
            case CodeSceneConstants.Titles.ViewTerms:
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://codescene.com/policies",
                    UseShellExecute = true
                });
                break;
        }
    }

    public void OnClosed(IVsInfoBarUIElement element) { }

    private void SendTelemetry(string eventName, string selection = "")
    {
        Task.Run(async () =>
        {
            var additionalData = new Dictionary<string, object>
            {
                { "selection", selection }
            };

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName, additionalData);
        }).FireAndForget();
    }
}