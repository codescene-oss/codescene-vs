using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.Win32;

namespace Codescene.VSExtension.VS2022.Application.Services;

[Export(typeof(AceAcknowledgementStateService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class AceAcknowledgementStateService
{
    [Import]
    private readonly ILogger _logger;

    private const string REGPATH = @"Software\Codescene\VSExtension";
    private const string REGKEY = "AceAcknowledged";

    public bool IsAcknowledged()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGPATH, writable: true)
                 ?? Registry.CurrentUser.CreateSubKey(REGPATH);
            object value = key.GetValue(REGKEY);

            var isAcknowledged = value is int intVal && intVal == 1;
            _logger.Debug($"ACE acknowledged: {isAcknowledged}");

            return isAcknowledged;
        }
        catch (Exception e)
        {
            _logger.Warn($"Could not retrieve ACE acknowledgement state: {e.Message}. Defaulting to false.");
            return false;
        }
    }

    public void SetAcknowledged()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGPATH, writable: true)
                 ?? Registry.CurrentUser.CreateSubKey(REGPATH);
            key.SetValue(REGKEY, 1, RegistryValueKind.DWord);
        }
        catch (Exception e)
        {
            _logger.Warn($"Failed to persist ACE acknowledgement state: {e.Message}.");
        }
    }
}
