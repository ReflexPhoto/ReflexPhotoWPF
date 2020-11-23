/*******************************************************/
/* Copyright ReflexPhoto © 2020 - Tous droits réservés */
/* https://reflexphoto.eu <dev@reflexphoto.eu>         */
/*******************************************************/
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace ReflexPhotoWPF
{
    /// <summary>
    /// Utility functions to make this application starts with Windows.
    /// </summary>
    internal static class StartWithWindows
    {
        internal static void InitAppStartsWithWindows()
        {
            bool? settingsStartWithWindows = SettingsStartWithWindows();
            InitStartWithWindowsInRegistry(settingsStartWithWindows, RegistryView.Registry32);
            InitStartWithWindowsInRegistry(settingsStartWithWindows, RegistryView.Registry64);
        }

        private static void InitStartWithWindowsInRegistry(bool? settingsStartWithWindows, RegistryView regView)
        {
            try
            {
                using (RegistryKey rkBase = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, regView))
                    if (rkBase != null)
                        using (RegistryKey rkApp = rkBase.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                            if (rkApp != null)
                            {
                                bool startWithWindows = rkApp.GetValue(App.ThisAssemblyName) != null; // Store current state (running at startup or not).
                                if (startWithWindows)
                                {
                                    // If app currently runs at Windows startup but settings asked to not.
                                    if (settingsStartWithWindows != null && settingsStartWithWindows.HasValue && !settingsStartWithWindows.Value)
                                        rkApp.DeleteValue(App.ThisAssemblyName, false); // Disable run at startup (= remove startup app key).
                                }
                                else
                                {
                                    // If app is not currently running at Windows startup and there are not settings stored.
                                    if (settingsStartWithWindows == null || !settingsStartWithWindows.HasValue)
                                        rkApp.SetValue(App.ThisAssemblyName, App.AssemblyLocationEscaped); // Make app runs at startup.
                                    else // Else if app is not currently running at Windows startup and there are settings stored.
                                    {
                                        // If settings asked to run at startup.
                                        if (settingsStartWithWindows.Value)
                                            rkApp.SetValue(App.ThisAssemblyName, App.AssemblyLocationEscaped); // Make app runs at startup.
                                    }
                                }
                            }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to make ReflexPhoto starts with Windows in registry. Exception=[" + ex.ToString() + "]");
            }
        }

        private static bool? SettingsStartWithWindows()
        {
            try
            {
                string savePath = App.SettingsFilepath;
                if (File.Exists(savePath))
                {
                    string saved = File.ReadAllText(savePath, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        string[] splitted = saved.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitted != null && splitted.Length == 5)
                            return splitted[3] == "True";
                    }
#if DEBUG
                    Debug.WriteLine("Failed to load settings from [" + savePath + "] (incorrect data).");
                }
                else
                    Debug.WriteLine("Failed to load settings from [" + savePath + "] (file not found).");
#else
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not load settings. Exception=[" + ex.ToString() + "].");
            }
            return null;
        }

        internal static bool SetStartWithWindows()
        {
            RegistrySet(RegistryView.Registry32, false);
            RegistrySet(RegistryView.Registry64, false);
            SetStartupTask(RegistryView.Registry32, false);
            SetStartupTask(RegistryView.Registry64, false);
            return true;
        }

        internal static bool UnsetStartWithWindows()
        {
            RegistrySet(RegistryView.Registry32, true);
            RegistrySet(RegistryView.Registry64, true);
            SetStartupTask(RegistryView.Registry32, true);
            SetStartupTask(RegistryView.Registry64, true);
            return true;
        }

        private static void SetStartupTask(RegistryView regView, bool unset = false)
        {
            try
            {
                using (RegistryKey rkBase = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, regView))
                    if (rkBase != null)
                        using (RegistryKey rkApp = rkBase.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run", true))
                            if (rkApp != null)
                            {
                                if (unset)
                                    rkApp.DeleteValue("ReflexPhotoWPF", false);
                                else
                                {
                                    byte[] bytes = { 02, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00 }; // Enable startup task byte code.
                                    rkApp.SetValue("ReflexPhotoWPF", bytes, RegistryValueKind.Binary);
                                }
                            }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to enable ReflexPhoto startup task in registry. Exception=[" + ex.ToString() + "]");
            }
        }

        private static bool RegistrySet(RegistryView regView, bool unset = false)
        {
            try
            {
                using (RegistryKey rkBase = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, regView))
                    if (rkBase != null)
                        using (RegistryKey rkApp = rkBase.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                            if (rkApp != null)
                            {
                                if (unset)
                                    rkApp.DeleteValue(App.ThisAssemblyName, false);
                                else
                                    rkApp.SetValue(App.ThisAssemblyName, App.AssemblyLocationEscaped);
                                return true;
                            }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to make ReflexPhoto starts with Windows in registry. Exception=[" + ex.ToString() + "]");
            }
            return false;
        }
    }
}
