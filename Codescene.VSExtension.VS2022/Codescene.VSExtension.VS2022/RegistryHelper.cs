using Microsoft.Win32;

namespace Codescene.VSExtension.VS2022
{
    class RegistryHelper
    {
        private const string REG_PATH = @"Software\Codescene\VSExtension";
        private const string REG_KEY = "HasRunBefore";
        public bool CheckIfHasRunBefore()
        {
            bool hasRunBefore = false;
            using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(REG_PATH))
            {
                object value = key.GetValue(REG_KEY);
                if (value is int intVal && intVal == 1)
                {
                    hasRunBefore = true;
                }
            }

            return hasRunBefore;
        }

        public void SetHasRunBefore()
        {
            using var key = Registry.CurrentUser.OpenSubKey(REG_PATH, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(REG_PATH);
            key.SetValue(REG_KEY, 1, RegistryValueKind.DWord);
        }
    }
}
