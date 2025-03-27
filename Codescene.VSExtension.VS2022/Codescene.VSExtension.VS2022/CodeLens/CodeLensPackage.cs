//using Microsoft.VisualStudio.Shell;
//using System;
//using System.Runtime.InteropServices;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Codescene.VSExtension.VS2022.CodeLens
//{
//    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
//    [Guid(PackageGuidString)]
//    public sealed class CodeLensPackage : AsyncPackage
//    {
//        public const string PackageGuidString = "f7e22ea4-4d07-4a48-aaca-d82ff13ec862";

//        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
//        {
//            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
//        }
//    }
//}
