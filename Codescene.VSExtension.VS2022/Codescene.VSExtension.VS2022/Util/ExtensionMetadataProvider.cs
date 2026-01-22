using Codescene.VSExtension.Core.Interfaces.Extension;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.Util;

[Export(typeof(IExtensionMetadataProvider))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class VsExtensionMetadataProvider : IExtensionMetadataProvider
{
    public string GetVersion() => Vsix.Version;
    public string GetDisplayName() => Vsix.Name;
    public string GetDescription() => Vsix.Description;
    public string GetPublisher() => Vsix.Author;
}