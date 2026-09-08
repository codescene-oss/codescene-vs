// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Codescene.VSExtension.Core.Interfaces;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Web.WebView2.Wpf;
using Window = EnvDTE.Window;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

internal sealed class WebView2AirspaceGuard : IVsWindowFrameEvents, IDisposable
{
    private readonly FrameworkElement _host;
    private readonly WebView2 _webView;
    private readonly ILogger _logger;
    private IVsUIShell7 _shell7;
    private DTE _dte;
    private WindowEvents _windowEvents;
    private uint _cookie;
    private bool _advised;
    private bool _disposed;
    private bool _updating;
    private bool _recalcQueued;

    public WebView2AirspaceGuard(FrameworkElement host, WebView2 webView, ILogger logger)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ThreadHelper.ThrowIfNotOnUIThread();

        var service = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell));
        _shell7 = service as IVsUIShell7;
        if (_shell7 != null)
        {
            _cookie = _shell7.AdviseWindowFrameEvents(this);
            _advised = true;
        }

        var dteService = ServiceProvider.GlobalProvider.GetService(typeof(DTE));
        _dte = dteService as DTE;
        if (_dte != null)
        {
            _windowEvents = _dte.Events.WindowEvents;
            _windowEvents.WindowActivated += OnDteWindowActivated;
            _windowEvents.WindowMoved += OnDteWindowMoved;
        }

        _host.SizeChanged += OnHostSizeChanged;
        QueueRecalculate();
    }

    public void OnFrameCreated(IVsWindowFrame frame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    public void OnFrameDestroyed(IVsWindowFrame frame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    public void OnFrameIsVisibleChanged(IVsWindowFrame frame, bool newIsVisible)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    public void OnFrameIsOnScreenChanged(IVsWindowFrame frame, bool newIsOnScreen)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    public void OnActiveFrameChanged(IVsWindowFrame oldFrame, IVsWindowFrame newFrame)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        ThreadHelper.ThrowIfNotOnUIThread();

        _host.SizeChanged -= OnHostSizeChanged;

        if (_windowEvents != null)
        {
            _windowEvents.WindowActivated -= OnDteWindowActivated;
            _windowEvents.WindowMoved -= OnDteWindowMoved;
            _windowEvents = null;
        }

        if (_advised && _shell7 != null)
        {
            _shell7.UnadviseWindowFrameEvents(_cookie);
            _advised = false;
        }

        _shell7 = null;
        _dte = null;
    }

    private static bool Intersects(RECT a, RECT b)
    {
        return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
    }

    private static bool IsMainWindowSized(RECT candidate, RECT ours)
    {
        var candidateW = candidate.Right - candidate.Left;
        var candidateH = candidate.Bottom - candidate.Top;
        var ourW = ours.Right - ours.Left;
        var ourH = ours.Bottom - ours.Top;
        return candidateW >= ourW * 0.9 && candidateH >= ourH * 0.9;
    }

    private static bool IsAutoHideToolWindow(Window window)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (window == null)
        {
            return false;
        }

        if (!window.Visible)
        {
            return false;
        }

        if (!window.AutoHides)
        {
            return false;
        }

        return !IsDocumentOrMainWindow(window.Type);
    }

    private static bool IsDocumentOrMainWindow(vsWindowType type)
    {
        return type == vsWindowType.vsWindowTypeDocument
               || type == vsWindowType.vsWindowTypeCodeWindow
               || type == vsWindowType.vsWindowTypeMainWindow;
    }

    private static bool TryGetDteWindowRect(Window window, RECT ours, out RECT rect)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        rect = default;

        if (TryGetHwndRect(window.HWnd, ours, out rect))
        {
            return true;
        }

        if (window.Width <= 0 || window.Height <= 0)
        {
            return false;
        }

        var left = window.Left;
        var top = window.Top;
        rect = new RECT
        {
            Left = left,
            Top = top,
            Right = left + window.Width,
            Bottom = top + window.Height,
        };

        return IsUsableOverlayRect(rect, ours);
    }

    private static bool TryGetHwndRect(IntPtr hwnd, RECT ours, out RECT rect)
    {
        rect = default;
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out rect))
        {
            return false;
        }

        return IsUsableOverlayRect(rect, ours);
    }

    private static bool IsUsableOverlayRect(RECT rect, RECT ours)
    {
        return HasArea(rect) && !IsMainWindowSized(rect, ours);
    }

    private static bool HasArea(RECT rect)
    {
        return rect.Right > rect.Left && rect.Bottom > rect.Top;
    }

    private void OnDteWindowActivated(Window gotFocus, Window lostFocus)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    private void OnDteWindowMoved(Window window, int top, int left, int width, int height)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        QueueRecalculate();
    }

    private void QueueRecalculate()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (_disposed || _recalcQueued)
        {
            return;
        }

        _recalcQueued = true;
        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                Recalculate();
                await Task.Yield();
                Recalculate();
            }
            finally
            {
                _recalcQueued = false;
            }
        }).FileAndForget("WebView2AirspaceGuard/QueueRecalculate");
    }

    private void Recalculate()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (!CanRecalculate())
        {
            return;
        }

        _updating = true;
        try
        {
            if (!TryGetWebViewRect(out var ourRect))
            {
                if (!TryGetHostRect(out ourRect))
                {
                    return;
                }
            }

            ApplyWebViewVisibility(HasOverlappingAutoHideWindow(ourRect));
        }
        catch (Exception ex)
        {
            _logger.Error("WebView2 airspace guard failed to update visibility.", ex);
        }
        finally
        {
            _updating = false;
        }
    }

    private bool CanRecalculate()
    {
        return !_disposed && !_updating;
    }

    private bool IsHostLaidOut()
    {
        return _host.ActualWidth > 0 && _host.ActualHeight > 0 && PresentationSource.FromVisual(_host) != null;
    }

    private void ApplyWebViewVisibility(bool hide)
    {
        var target = hide ? Visibility.Hidden : Visibility.Visible;
        if (_webView.Visibility == target)
        {
            return;
        }

        _webView.Visibility = target;

#pragma warning disable VSTHRD001
        _webView.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
#pragma warning restore VSTHRD001

        var hwnd = GetWebViewHwnd();
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(hwnd, hide ? NativeMethods.SwHide : NativeMethods.SwShow);
        }
    }

    private bool HasOverlappingAutoHideWindow(RECT ourRect)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (_dte?.Windows == null)
        {
            return false;
        }

        foreach (Window window in _dte.Windows)
        {
            if (!IsAutoHideToolWindow(window) || !TryGetDteWindowRect(window, ourRect, out var overlay))
            {
                continue;
            }

            if (Intersects(ourRect, overlay))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetHostRect(out RECT rect)
    {
        rect = default;
        if (!IsHostLaidOut())
        {
            return false;
        }

        var topLeft = _host.PointToScreen(new Point(0, 0));
        var bottomRight = _host.PointToScreen(new Point(_host.ActualWidth, _host.ActualHeight));
        rect = new RECT
        {
            Left = (int)topLeft.X,
            Top = (int)topLeft.Y,
            Right = (int)bottomRight.X,
            Bottom = (int)bottomRight.Y,
        };

        return HasArea(rect);
    }

    private bool TryGetWebViewRect(out RECT rect)
    {
        rect = default;
        var hwnd = GetWebViewHwnd();
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out rect))
        {
            return false;
        }

        return HasArea(rect);
    }

    private IntPtr GetWebViewHwnd()
    {
        try
        {
            return _webView.Handle;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private static class NativeMethods
    {
        internal const int SwHide = 0;
        internal const int SwShow = 5;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
