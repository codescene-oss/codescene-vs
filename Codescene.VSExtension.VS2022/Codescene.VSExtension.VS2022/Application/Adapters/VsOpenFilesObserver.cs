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
    [Export(typeof(IOpenFilesObserver))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsOpenFilesObserver : IOpenFilesObserver
    {
        public IEnumerable<string> GetAllVisibleFileNames()
        {
            var result = new List<string>();

            try
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    var rdt = ServiceProvider.GlobalProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                    if (rdt == null)
                    {
                        return;
                    }

                    rdt.GetRunningDocumentsEnum(out IEnumRunningDocuments enumerator);
                    if (enumerator == null)
                    {
                        return;
                    }

                    uint[] cookies = new uint[1];
                    while (true)
                    {
                        int hr = enumerator.Next(1, cookies, out uint fetched);
                        if (hr != VSConstants.S_OK || fetched == 0)
                        {
                            break;
                        }

                        rdt.GetDocumentInfo(
                            cookies[0],
                            out uint flags,
                            out uint readLocks,
                            out uint editLocks,
                            out string moniker,
                            out IVsHierarchy hierarchy,
                            out uint itemId,
                            out IntPtr docData);

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
            }
            catch
            {
            }

            return result;
        }
    }
}
