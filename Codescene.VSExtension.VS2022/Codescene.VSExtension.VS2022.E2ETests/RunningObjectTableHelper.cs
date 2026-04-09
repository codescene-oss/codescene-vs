// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;

namespace Codescene.VSExtension.VS2022.E2ETests;

internal static class RunningObjectTableHelper
{
    public static object WaitForDte(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            var dte = TryGetDte(processId);
            if (dte != null)
            {
                return dte;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }

        throw new TimeoutException($"Could not acquire a Visual Studio DTE object for process {processId} within {timeout}.");
    }

    private static object? TryGetDte(int processId)
    {
        GetRunningObjectTable(0, out var runningObjectTable);
        runningObjectTable.EnumRunning(out var enumMoniker);
        var monikers = new IMoniker[1];

        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            CreateBindCtx(0, out var bindContext);
            monikers[0].GetDisplayName(bindContext, null, out var displayName);

            if (!displayName.StartsWith("!VisualStudio.DTE", StringComparison.Ordinal))
            {
                Release(bindContext);
                Release(monikers[0]);
                continue;
            }

            if (!TryParseProcessId(displayName, out var parsedProcessId) || parsedProcessId != processId)
            {
                Release(bindContext);
                Release(monikers[0]);
                continue;
            }

            runningObjectTable.GetObject(monikers[0], out var dte);
            Release(bindContext);
            Release(monikers[0]);
            Release(enumMoniker);
            Release(runningObjectTable);
            return dte;
        }

        Release(enumMoniker);
        Release(runningObjectTable);
        return null;
    }

    private static bool TryParseProcessId(string displayName, out int processId)
    {
        var separator = displayName.LastIndexOf(':');
        if (separator < 0)
        {
            processId = 0;
            return false;
        }

        return int.TryParse(displayName.Substring(separator + 1), out processId);
    }

    private static void Release(object? value)
    {
        if (value != null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx bindContext);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable runningObjectTable);
}
