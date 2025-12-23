using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Settings;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.Options
{
    [Export(typeof(ISettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GeneralSettingsProvider : ISettingsProvider
    {
        public bool ShowDebugLogs => General.Instance.ShowDebugLogs;
        public string AuthToken 
        { 
            get 
            {
                var token = General.Instance.AuthToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
                }
                return token;
            }
        }
    }
}