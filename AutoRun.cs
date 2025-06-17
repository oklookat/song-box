using Microsoft.Win32;

namespace song_box
{
    internal class AutoRun
    {
        private const string RegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void Add(string appName, string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath, true))
            {
                key.SetValue(appName, $"\"{exePath}\"");
            }
        }

        public static void Remove(string appName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath, true))
            {
                if (key.GetValue(appName) != null)
                    key.DeleteValue(appName);
            }
        }

        public static bool Exists(string appName)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegPath, false))
            {
                return key.GetValue(appName) != null;
            }
        }
    }
}
