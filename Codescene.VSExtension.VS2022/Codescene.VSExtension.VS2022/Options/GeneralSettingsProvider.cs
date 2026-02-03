using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;

namespace Codescene.VSExtension.VS2022.Options
{
    [Export(typeof(ISettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GeneralSettingsProvider : ISettingsProvider
    {
        public bool ShowDebugLogs => General.Instance.ShowDebugLogs;

        public string AuthToken => General.Instance.AuthToken;
    }
}
