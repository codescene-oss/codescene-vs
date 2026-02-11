// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Git;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IOpenFilesSource))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsOpenFilesSource : IOpenFilesSource
    {
        public IEnumerable<string> GetOpenDocumentPaths()
        {
            var result = new List<string>();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var rdt = ServiceProvider.GlobalProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                if (rdt == null)
                {
                    return;
                }

                rdt.GetRunningDocumentsEnum(out var enumerator);
                if (enumerator == null)
                {
                    return;
                }

                var cookies = new uint[1];
                while (true)
                {
                    var hr = enumerator.Next(1, cookies, out var fetched);
                    if (hr != VSConstants.S_OK || fetched == 0)
                    {
                        break;
                    }

                    rdt.GetDocumentInfo(
                        cookies[0],
                        out _,
                        out _,
                        out _,
                        out var moniker,
                        out _,
                        out _,
                        out var docData);

                    if (docData != IntPtr.Zero)
                    {
                        System.Runtime.InteropServices.Marshal.Release(docData);
                    }

                    if (!string.IsNullOrEmpty(moniker))
                    {
                        result.Add(moniker);
                    }
                }
            });

            return result;
        }
    }
}
