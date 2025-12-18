using Microsoft.Win32;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.Application.Services;

[Export(typeof(AceAcknowledgementStateService))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class AceAcknowledgementStateService
{
    private const string REG_PATH = @"Software\Codescene\VSExtension";
    private const string REG_KEY = "AceAcknowledged";

    public bool IsAcknowledged()
    {
        bool acknowledged = false;
        using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(REG_PATH))
        {
            object value = key.GetValue(REG_KEY);
            if (value is int intVal && intVal == 1)
            {
                acknowledged = true;
            }
        }

        return acknowledged;
    }

    public void SetAcknowledged()
    {
        using var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(REG_PATH);
        key.SetValue(REG_KEY, 1, RegistryValueKind.DWord);
    }
}

