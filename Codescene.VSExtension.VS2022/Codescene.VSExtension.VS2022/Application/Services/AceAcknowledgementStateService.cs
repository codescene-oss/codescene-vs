using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Microsoft.Win32;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.Application.Services;

[Export(typeof(AceAcknowledgementStateService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class AceAcknowledgementStateService
{
    [Import]
    private readonly ILogger _logger;

    private const string REG_PATH = @"Software\Codescene\VSExtension";
    private const string REG_KEY = "AceAcknowledged";

    public bool IsAcknowledged()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                 ?? Registry.CurrentUser.CreateSubKey(REG_PATH);
            object value = key.GetValue(REG_KEY);

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
            using var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                 ?? Registry.CurrentUser.CreateSubKey(REG_PATH);
            key.SetValue(REG_KEY, 1, RegistryValueKind.DWord);
        }
        catch (Exception e)
        {
            _logger.Warn($"Failed to persist ACE acknowledgement state: {e.Message}.");
        }
    }
}

