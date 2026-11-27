// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.VS2022.Options;
using Codescene.VSExtension.VS2022.ToolWindows;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.Application
{
    [Export(typeof(IRefactoringHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsRefactoringHost : IRefactoringHost
    {
        private readonly ILogger _logger;

        [ImportingConstructor]
        public VsRefactoringHost(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsDirty(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return false;
            }

            try
            {
                var persistDocData = FindPersistDocData(path);
                if (persistDocData != null)
                {
                    persistDocData.IsDocDataDirty(out var isDirty);
                    return isDirty != 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error checking if document is dirty: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConfirmSaveAsync(string fileName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var message = $"Save {fileName} before refactoring? The refactoring agent reads the file from disk.";
            var result = await VS.MessageBox.ShowAsync(
                message,
                buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL);

            return result == Microsoft.VisualStudio.VSConstants.MessageBoxResult.IDOK;
        }

        public async Task SaveAsync(string path)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                var persistDocData = FindPersistDocData(path);
                if (persistDocData != null)
                {
                    persistDocData.SaveDocData(
                        (VSSAVEFLAGS)__VSRDTSAVEOPTIONS.RDTSAVEOPT_ForceSave,
                        out _,
                        out _);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error saving document: {ex.Message}", ex);
            }
        }

        public async Task OfferSetApiKeyAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var result = await VS.MessageBox.ShowAsync(
                message,
                buttons: Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL);

            if (result == Microsoft.VisualStudio.VSConstants.MessageBoxResult.IDOK)
            {
                await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();
            }
        }

        public async Task ShowReviewAsync(AceComponentData data)
        {
            await AceToolWindow.ShowAsync();
            await UpdateReviewAsync(data);
        }

        public async Task UpdateReviewAsync(AceComponentData data)
        {
            await AceToolWindow.UpdateViewAsync(new WebComponentMessage<AceComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = WebComponentPayload<AceComponentData>.Create(
                    ViewTypes.ACE,
                    data),
            });
        }

        public string ReadDocumentText(string path)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return ReadDocumentTextOnMainThread(path);
            });
        }

        private static IVsPersistDocData FindPersistDocData(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(Package.GetGlobalService(typeof(SVsRunningDocumentTable)) is IVsRunningDocumentTable rdt))
            {
                return null;
            }

            var hr = rdt.FindAndLockDocument(
                (uint)_VSRDTFLAGS.RDT_NoLock,
                path,
                out _,
                out _,
                out var docData,
                out _);

            if (hr != Microsoft.VisualStudio.VSConstants.S_OK || docData == System.IntPtr.Zero)
            {
                return null;
            }

            return System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData) as IVsPersistDocData;
        }

        private string ReadDocumentTextOnMainThread(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var rdt = Package.GetGlobalService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                if (rdt == null)
                {
                    return System.IO.File.ReadAllText(path);
                }

                var hr = rdt.FindAndLockDocument(
                    (uint)_VSRDTFLAGS.RDT_NoLock,
                    path,
                    out _,
                    out _,
                    out var docData,
                    out _);

                if (hr == Microsoft.VisualStudio.VSConstants.S_OK && docData != System.IntPtr.Zero)
                {
                    var textLines = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData) as IVsTextLines;
                    if (textLines != null)
                    {
                        textLines.GetLastLineIndex(out var lastLine, out var lastIndex);
                        textLines.GetLineText(0, 0, lastLine, lastIndex, out var text);
                        return text ?? string.Empty;
                    }
                }

                return System.IO.File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error reading document text: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
