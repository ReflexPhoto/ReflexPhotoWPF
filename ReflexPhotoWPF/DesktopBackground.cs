/*******************************************************/
/* Copyright ReflexPhoto © 2020 - Tous droits réservés */
/* https://reflexphoto.eu <dev@reflexphoto.eu>         */
/*******************************************************/
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace ReflexPhotoWPF
{
    internal enum WallpaperAspect
    {
        FILLED = 0,
        FITED = 1,
        STRETCHED = 2,
        CENTERED = 3,
        TILED = 4,
        EXTENDED = 5
    }

    internal class DesktopBackground
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, String pvParam, uint fWinIni);

        private const uint SPI_SETDESKWALLPAPER = 0x14; // 20
        private const uint SPIF_UPDATEINIFILE = 0x01; // 1
        private const uint SPIF_SENDWININICHANGE = 0x02; // 2

        internal const WallpaperAspect DefaultAspectRatio = WallpaperAspect.FITED;
        internal static string Setting_TileWallpaper = "0";
        internal static string Setting_WallpaperStyle = "6";

        internal static void SetWallpaperAspectRatio(WallpaperAspect ratio = DefaultAspectRatio)
        {
            switch (ratio)
            {
                case WallpaperAspect.EXTENDED:
                    Setting_WallpaperStyle = "22";
                    Setting_TileWallpaper = "0";
                    break;
                case WallpaperAspect.FILLED:
                    Setting_WallpaperStyle = "10";
                    Setting_TileWallpaper = "0";
                    break;
                case WallpaperAspect.FITED:
                    Setting_WallpaperStyle = "6";
                    Setting_TileWallpaper = "0";
                    break;
                case WallpaperAspect.STRETCHED:
                    Setting_WallpaperStyle = "2";
                    Setting_TileWallpaper = "0";
                    break;
                case WallpaperAspect.CENTERED:
                    Setting_WallpaperStyle = "0";
                    Setting_TileWallpaper = "0";
                    break;
                case WallpaperAspect.TILED:
                    Setting_WallpaperStyle = "0";
                    Setting_TileWallpaper = "1";
                    break;
                default: // Fallback to FITED
                    Setting_WallpaperStyle = "6";
                    Setting_TileWallpaper = "0";
                    break;
            }
        }

        // Display the file on the desktop.
        internal static string DisplayPicture(string file_name, bool update_registry)
        {
            try
            {
#if DEBUG
                Debug.WriteLine("Displaying image [" + file_name + "].");
#endif
                // If we should update the registry, set the appropriate flags.
                uint flags = 0;
                if (update_registry)
                    flags = SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE;

                // Make background fit screen.
                MakeBackgroundFitScreenInRegistry(RegistryView.Registry32);
                MakeBackgroundFitScreenInRegistry(RegistryView.Registry64);

                // Set the desktop background to this file.
                if (!SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, file_name, flags))
                    return "Impossible de définir l'image \"" + file_name + "\" comme fond d'écran. Exception=[Call to SystemParametersInfo() failed]";
            }
            catch (Exception ex)
            {
                return "Impossible de définir l'image \"" + file_name + "\" comme fond d'écran. Exception=[" + ex.ToString() + "]";
            }
            return null;
        }

        internal static int? SettingsWallpaperAspectRatio()
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
                            if (int.TryParse(splitted[4], out int aspectRatio) && aspectRatio >= 0 && aspectRatio <= 5)
                                return aspectRatio;
                    }
#if DEBUG
                    Debug.WriteLine("Failed to load aspect ratio setting from [" + savePath + "] (incorrect data).");
                }
                else
                    Debug.WriteLine("Failed to load aspect ratio setting from [" + savePath + "] (file not found).");
#else
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not load aspect ratio setting. Exception=[" + ex.ToString() + "].");
            }
            return null;
        }

        private static void MakeBackgroundFitScreenInRegistry(RegistryView regType)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, regType))
                    if (baseKey != null)
                        using (RegistryKey key = baseKey.OpenSubKey("Control Panel\\Desktop", true))
                            if (key != null)
                            {
                                key.SetValue("WallpaperStyle", Setting_WallpaperStyle); // Make wallpaper fit screen (without modifying its width/height ratio).
                                key.SetValue("TileWallpaper", Setting_TileWallpaper); // Display wallpaper only once (no tiles).
                            }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to make background fit screen in registry. Exception=[" + ex.ToString() + "]");
            }
        }
    }
}
