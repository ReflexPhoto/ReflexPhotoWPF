using Hardcodet.Wpf.TaskbarNotification;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using Timer = System.Timers.Timer;

namespace ReflexPhotoWPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string MUTEX_ID = "ReflexPhotoSystrayExtensionMutex";

        private TaskbarIcon notifyIcon;

        private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
        internal static readonly string ThisAssemblyName = CurrentAssembly.GetName().Name;
        internal static readonly string AssemblyLocation = CurrentAssembly.Location.Replace(".dll", ".exe");
        internal static readonly string AssemblyLocationEscaped = "\"" + AssemblyLocation + "\"";

        internal static string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReflexPhoto");
        private static readonly string CacheFolder = Path.Combine(AppFolder, "Cache");
        internal static readonly string SettingsFilepath = Path.Combine(AppFolder, "settings.txt");

        private const string BaseUrl = "https://reflexphoto.eu";

        private static Timer aTimer = null;
        private static HttpClient Client = null;
        private static readonly Dictionary<string, bool> DisplayUrls = new Dictionary<string, bool>();

        internal const int MIN_SECONDS = 30; // Minimum number of seconds to wait before background refresh. Must be in [1;59] interval.
        private const int KEEP_IN_CACHE_FOR = 12; // Keep images in Cache folder for 12 hour.
        private const int MAX_FILES_IN_CACHE = 20; // Never keep more than 20 files at the same time in Cache folder.

        // Used to extract paragraph.
        private const string SEPARATOR_A = "<div id=\"imageurls\" class=\"content\"><p>";
        private const string SEPARATOR_B = "</p></div>";
        // Used to extract URLs.
        private const string SEPARATOR_C = "<a href=\"";
        private const string SEPARATOR_D = "\" rel=\"";
        // Used to identify gallery image URLs.
        private const string SEPARATOR_E = BaseUrl + "/gallery/image/";
        private static readonly int SEPARATOR_E_LEN = SEPARATOR_E.Length;
        private const string SEPARATOR_F = "/source";
        // Used to identify download image URLs.
        private const string SEPARATOR_G = BaseUrl + "/download/file.php?id=";
        private static readonly int SEPARATOR_G_LEN = SEPARATOR_G.Length;

        // Settings.
        private static int SettingTypeFondEcran = 0;
        private static int SettingChangeInterval = 1;
        private static int SettingChangeIntervalType = 1;
        private static WallpaperAspect SettingWallpaperAspectRatio = DesktopBackground.DefaultAspectRatio;
#pragma warning disable CS0414 // Unused member.
#pragma warning disable IDE0052 // Unused member.
        private static bool SettingStartsWithWindows = true;
#pragma warning restore IDE0052
#pragma warning restore CS0414

        private static Mutex mutex = null;

        internal static void StopApp(int exitCode)
        {
            if (mutex != null)
                mutex.Close(); // Release mutex.
            Current.Shutdown(exitCode);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            notifyIcon.Dispose(); // The icon would clean up automatically, but this is cleaner.
            base.OnExit(e);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // If systray app is already running.
            if (Mutex.TryOpenExisting(MUTEX_ID, out mutex)) // Prevents multiple executions.
                Current.Shutdown(0);
            else
            {
                // Open mutex.
                mutex = new Mutex(false, MUTEX_ID);
#if DEBUG
                Debug.WriteLine("ReflexPhoto Desktop is starting.");
#endif

                // If the application was started from the MSI installer.
                if (e.Args.Length >= 1 && (e.Args[0] == "/Commit" || e.Args[0] == "Commit"))
                {
                    // Try launch itself again but in another process (so that MSI installer can continue to success screen).
                    Process runSelf = null;
                    try
                    {
                        runSelf = new Process();
                        runSelf.StartInfo.FileName = AssemblyLocation;
                        runSelf.Start();
                    }
#if DEBUG
                    catch (Exception ex)
#else
                    catch
#endif
                    {
                        // Cleanup any running instance on failure.
                        if (runSelf != null)
                        {
                            try { if (!runSelf.HasExited) { runSelf.CloseMainWindow(); } } catch { }
                            try { runSelf.Close(); } catch { }
                        }
#if DEBUG
                        Debug.WriteLine("ERROR: Unable to open configurator. Exception=[" + ex.ToString() + "]");
#endif
                    }
                    finally
                    {
                        StopApp(0); // Exit with success code 0.
                    }
                }

                // Create our root directory to store various files.
                if (!Directory.Exists(AppFolder))
                    Directory.CreateDirectory(AppFolder);
                if (!Directory.Exists(AppFolder))
                {
#if DEBUG
                    Debug.WriteLine("Failed to create main directory.");
#endif
                    // Exit with failure code -1.
                    StopApp(-1);
                }

                // Get the systray icon (it's a resource declared in NotifyIconResources.xaml).
                notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
                notifyIcon.PreviewMouseDown += NotifyIcon_PreviewMouseDown;
                notifyIcon.MouseDown += NotifyIcon_MouseDown;
                notifyIcon.TrayBalloonTipClicked += NotifyIcon_TrayBalloonTipClicked;
                notifyIcon.TrayMouseDoubleClick += NotifyIcon_TrayMouseDoubleClick;

                // Initialize HTTP client.
                InitializeHttpClient();
                if (Client != null)
                {
                    // Make this app starts with Windows.
                    StartWithWindows.InitAppStartsWithWindows();
                    // Start looping.
                    StartLoop();
                }
            }
        }

        private void NotifyIcon_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenWindow();
        }

        private void NotifyIcon_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenWindow();

        private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e) => OpenWindow();

        private void NotifyIcon_TrayBalloonTipClicked(object sender, RoutedEventArgs e) => OpenWindow();

        private void OpenWindow()
        {
            if (App.Current.MainWindow != null)
            {
                App.Current.MainWindow.Close();
                App.Current.MainWindow = null;
            }
            if (App.Current.MainWindow == null)
            {
                App.Current.MainWindow = new MainWindow();
                App.Current.MainWindow.Show();
            }
        }

        // Refresh menu as soon as it opens.
        //private void ContextMenu_Opened(object sender, RoutedEventArgs e) => CommandManager.InvalidateRequerySuggested();

        private void InitializeHttpClient()
        {
            if (Client == null)
            {
                Client = new HttpClient()
                {
                    BaseAddress = new Uri(BaseUrl),
                    Timeout = new TimeSpan(0, 0, 15)
                };
                if (Client != null)
                {
                    Client.DefaultRequestHeaders.Add("Accept", "text/html");
                    Client.DefaultRequestHeaders.Add("User-Agent", "ReflexPhotoWallpaper/1.0");
                }
            }
        }

        private static void StartLoop() => RestartLoop();

        public static void RestartLoop()
        {
            // Stop and clean old timer if any.
            if (aTimer != null)
            {
                aTimer.AutoReset = false;
                aTimer.Stop();
                aTimer.Close();
                aTimer.Dispose();
                aTimer = null;
            }
            // Load settings.
            LoadSettings();
            // Setup aspect ratio in DesktopBackground.
            DesktopBackground.SetWallpaperAspectRatio(SettingWallpaperAspectRatio);
            // Initialize timer.
            double ms;
            if (SettingChangeIntervalType == 0)
                ms = SettingChangeInterval * 24 * 60 * 60 * 1000;
            else if (SettingChangeIntervalType == 1)
                ms = SettingChangeInterval * 60 * 60 * 1000;
            else if (SettingChangeIntervalType == 2)
                ms = SettingChangeInterval * 60 * 1000;
            else if (SettingChangeIntervalType == 3)
                ms = SettingChangeInterval * 1000;
            else // Fallback default to hours
                ms = SettingChangeInterval * 60 * 60 * 1000;
            // Call UpdateBackground every ms milliseconds.
            aTimer = new Timer(ms);
            aTimer.Elapsed += ATimer_Elapsed;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
            // Also call UpdateBackground once immediately.
#pragma warning disable CS4014 // No need to wait.
            UpdateBackground();
#pragma warning restore CS4014
        }

#pragma warning disable CS4014 // No need to wait.
        private static void ATimer_Elapsed(object sender, ElapsedEventArgs e) => UpdateBackground();
#pragma warning restore CS4014

        private static async Task<string> GetPage(string url)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (request != null)
                    using (HttpResponseMessage response = await Client.SendAsync(request))
                    {
                        if (response != null && response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null)
                            return await response.Content.ReadAsStringAsync();
                    }
            }
            return null;
        }

        private static async Task FillDisplayUrls()
        {
            string contentStr;
            if (SettingTypeFondEcran == 1) // Page d'accueil : BaseUrl + /fondecran.php?type=2
                contentStr = await GetPage("/fondecran.php?type=2");
            else // Best-Of : BaseUrl + /fondecran.php?type=1
                contentStr = await GetPage("/fondecran.php?type=1");
            if (!string.IsNullOrWhiteSpace(contentStr) && contentStr.Contains(SEPARATOR_A))
            {
                int stt = contentStr.IndexOf(SEPARATOR_A);
                if (stt > 0)
                {
                    stt += SEPARATOR_A.Length;
                    int end = contentStr.IndexOf(SEPARATOR_B, stt);
                    if (end > stt)
                    {
                        string content = contentStr.Substring(stt, (end - stt));
                        string[] splitted = content.Split(new string[] { SEPARATOR_C }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> urls = new List<string>();
                        if (splitted != null && splitted.Length > 0)
                            foreach (string split in splitted)
                                if (split.StartsWith(BaseUrl) && split.Contains(SEPARATOR_D))
                                {
                                    int splitEnd = split.IndexOf(SEPARATOR_D);
                                    if (splitEnd > 0)
                                        urls.Add(split.Substring(0, splitEnd));
                                }
                        if (urls.Count > 0)
                        {
                            foreach (string url in urls)
                            {
                                if (!DisplayUrls.ContainsKey(url))
                                    DisplayUrls.Add(url, false);
#if DEBUG
                                Debug.WriteLine(url);
                            }
                        }
                        else
                            Debug.WriteLine("No images returned from ReflexPhoto website.");
                    }
                    else
                        Debug.WriteLine("Could not get images from ReflexPhoto website (incorrect data).");
                }
                else
                    Debug.WriteLine("Could not get images from ReflexPhoto website (bad data).");
            }
            else
                Debug.WriteLine("Could not get images from ReflexPhoto website (no connectivity).");
#else
                            }
                        }
                    }
                }
            }
#endif
        }

        private static DateTime? LastDisplayListCleanup = null;

        private static async Task UpdateBackground()
        {
#if DEBUG
            try
            {
#endif
                if (SettingTypeFondEcran == 1) // Page d'accueil
                {
                    // Refresh display list every 5 minutes when it's between 17:00 and 19:59.
                    if (DateTime.Now.Hour >= 17 && DateTime.Now.Hour <= 19)
                    {
                        if (LastDisplayListCleanup == null || (LastDisplayListCleanup.Value.AddMinutes(5) < DateTime.Now))
                        {
                            LastDisplayListCleanup = DateTime.Now;
                            DisplayUrls.Clear();
                        }
                    }
                    if (DisplayUrls.Count <= 0)
                        await FillDisplayUrls();
                    if (DisplayUrls.Count > 0)
                    {
                        string url = null;
                        foreach (KeyValuePair<string, bool> cachedUrl in DisplayUrls)
                            if (!cachedUrl.Value)
                            {
                                url = cachedUrl.Key;
                                break;
                            }
                        if (url == null)
                        {
                            string[] cachedUrlKeys = DisplayUrls.Keys.ToArray();
                            if (cachedUrlKeys != null)
                                foreach (string cachedUrlKey in cachedUrlKeys)
                                    DisplayUrls[cachedUrlKey] = false;
                        }
                        foreach (KeyValuePair<string, bool> cachedUrl in DisplayUrls)
                            if (!cachedUrl.Value)
                            {
                                url = cachedUrl.Key;
                                break;
                            }
                        if (url != null && DisplayUrls.ContainsKey(url) && !DisplayUrls[url])
                        {
                            DisplayUrls[url] = true;
                            await DisplayImage(url);
                        }
#if DEBUG
                        else
                            Debug.WriteLine("Failed to get a CDC background image.");
                    }
                    else
                        Debug.WriteLine("Could not get a CDC background image.");
#else
                    }
#endif
                }
                else // Best-Of
                {
                    string url = null;
                    int cnt = 0;
                    while (url == null && cnt < 2)
                    {
                        // Try get image from display list.
                        foreach (KeyValuePair<string, bool> cachedUrl in DisplayUrls)
                            if (!cachedUrl.Value)
                            {
                                url = cachedUrl.Key;
                                break;
                            }
                        // If no image was available in display list, try to get some fresh URLs from website into the display list.
                        if (url == null)
                            await FillDisplayUrls();
                        ++cnt;
                    }
                    // If we were not able to get an image even after filling display list, that means we've displayed every Best-Of image.
                    if (url == null && DisplayUrls.Count > 0)
                    {
                        // Reset values.
                        string[] keys = DisplayUrls.Keys.ToArray();
                        if (keys != null)
                            foreach (string key in keys)
                                if (DisplayUrls.ContainsKey(key))
                                    DisplayUrls[key] = false;
                        // Try get image to display.
                        foreach (KeyValuePair<string, bool> cachedUrl in DisplayUrls)
                            if (!cachedUrl.Value)
                            {
                                url = cachedUrl.Key;
                                break;
                            }
                    }
                    if (url != null && DisplayUrls.ContainsKey(url) && !DisplayUrls[url])
                    {
                        DisplayUrls[url] = true;
                        await DisplayImage(url);
                    }
#if DEBUG
                    else
                        Debug.WriteLine("Failed to get a Best-Of background image.");
#endif
                }
#if DEBUG
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
#endif
        }

        private static async Task<string> GetImage(string url)
        {
            if (url.StartsWith(BaseUrl))
            {
                string fileName = null;
                if (url.StartsWith(SEPARATOR_E) && url.Length > SEPARATOR_E_LEN)
                {
                    int end = url.IndexOf(SEPARATOR_F, SEPARATOR_E_LEN);
                    if (end > SEPARATOR_E_LEN && url.Length > end)
                        fileName = "g" + url.Substring(SEPARATOR_E_LEN, (end - SEPARATOR_E_LEN));
                }
                else if (url.StartsWith(SEPARATOR_G) && url.Length > SEPARATOR_G_LEN)
                    fileName = "f" + url.Substring(SEPARATOR_G_LEN);
                if (fileName != null)
                {
                    string cacheFolderPath = CacheFolder;
                    bool dirExist = Directory.Exists(cacheFolderPath);
                    if (!dirExist)
                    {
                        try
                        {
                            DirectoryInfo di = Directory.CreateDirectory(cacheFolderPath);
                            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to create folder at [" + cacheFolderPath + "]. Exception=[" + ex.ToString() + "]");
                        }
                        dirExist = Directory.Exists(cacheFolderPath);
                    }
                    if (dirExist)
                    {
                        // Try get image from cache folder.
                        string[] files = null;
                        try { files = Directory.GetFiles(cacheFolderPath, "*", SearchOption.TopDirectoryOnly); }
                        catch { files = null; }
                        if (files != null && files.Length > 0)
                            foreach (string file in files)
                                if (Path.GetFileNameWithoutExtension(file) == fileName)
                                {
                                    FileInfo fi = null;
                                    try { fi = new FileInfo(Path.GetFullPath(file)); }
                                    catch { fi = null; }
                                    if (fi != null)
                                    {
#if DEBUG
                                        Debug.WriteLine("Image found in cache at [" + fi.FullName + "].");
#endif
                                        return fi.FullName;
                                    }
                                }

                        // If image not found in cache folder, download it.
                        int retry = 0;
                        while (retry < 3)
                        {
                            try
                            {
                                using (HttpResponseMessage response = await Client.GetAsync(url.Substring(BaseUrl.Length)))
                                {
                                    if (response != null && response.StatusCode == System.Net.HttpStatusCode.OK && response.Content != null && response.Content.Headers?.ContentType?.MediaType != null)
                                    {
                                        //string origFileName = Path.GetExtension(response.Content.Headers.ContentDisposition.FileNameStar);
                                        string fileExtension = response.Content.Headers.ContentType.MediaType == "image/png" ? ".png" : (response.Content.Headers.ContentType.MediaType == "image/gif" ? ".gif" : (response.Content.Headers.ContentType.MediaType == "image/jpeg" ? ".jpg" : string.Empty));
                                        if (fileExtension.Length > 0)
                                        {
                                            string fullFileName = fileName + fileExtension;
                                            FileInfo fileInfo = new FileInfo(Path.Combine(cacheFolderPath, fullFileName));
                                            if (fileInfo != null)
                                            {
                                                using (Stream ms = await response.Content.ReadAsStreamAsync())
                                                {
                                                    using (FileStream fs = File.Create(fileInfo.FullName))
                                                    {
                                                        ms.Seek(0, SeekOrigin.Begin);
                                                        ms.CopyTo(fs);
                                                        ms.Flush();
                                                        fs.Flush();
                                                    }
                                                }
#if DEBUG
                                                Debug.WriteLine("Image downloaded and saved at [" + fileInfo.FullName + "].");
#endif
                                                return fileInfo.FullName;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Failed to download image at [" + url + "]. Retrying. Exception=[" + ex.ToString() + "]");
                            }
                            ++retry;
                        }
                    }
                }
            }
            return null;
        }

        private static void ClearCache(string filePath)
        {
            string cacheFolderPath = CacheFolder;
            if (Directory.Exists(cacheFolderPath))
            {
                // Remove all files from Cache folder except given filePath.
                string[] files;
                try { files = Directory.GetFiles(cacheFolderPath, "*", SearchOption.TopDirectoryOnly); }
                catch { files = null; }
                if (files != null && files.Length > 0)
                {
                    List<string> toDel = new List<string>();
                    Dictionary<string, DateTime> remaining = new Dictionary<string, DateTime>();

                    // Delete files older than KEEP_IN_CACHE.
                    foreach (string file in files)
                    {
                        string fullPath = Path.GetFullPath(file);
                        if (Path.GetFileName(file) != Path.GetFileName(filePath))
                        {
                            DateTime creationDate = File.GetCreationTime(fullPath);
                            if (creationDate.AddHours(KEEP_IN_CACHE_FOR) < DateTime.Now)
                                toDel.Add(fullPath);
                            else
                                remaining.Add(fullPath, File.GetCreationTime(fullPath));
                        }
                        else
                            remaining.Add(fullPath, File.GetCreationTime(fullPath));
                    }
                    if (toDel.Count > 0)
                        foreach (string d in toDel)
                            if (File.Exists(d))
                            {
                                try { File.Delete(d); }
                                catch { }
                            }

                    // Delete files till it respects MAX_FILES_IN_CACHE
                    if (remaining.Count > MAX_FILES_IN_CACHE)
                    {
                        int toTake = remaining.Count - MAX_FILES_IN_CACHE;
                        remaining.OrderBy(kp => kp.Value).Select(kp => kp.Key).Take(toTake).ToList().ForEach(k =>
                        {
                            if (File.Exists(k))
                                try { File.Delete(k); }
                                catch { }
                        });
                    }
                }
            }
        }

        private static async Task DisplayImage(string url)
        {
            string filePath = await GetImage(url);
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                Task.Delay(200).Wait(400);
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    FileInfo fi;
                    try { fi = new FileInfo(filePath); }
                    catch { fi = null; }
                    if (fi != null && fi.Exists && File.Exists(fi.FullName))
                    {
                        // Clear cache.
                        ClearCache(fi.FullName);
                        // Display desktop background image.
#if DEBUG
                        string ret = DesktopBackground.DisplayPicture(fi.FullName, true);
                        if (ret != null)
                            Debug.WriteLine(ret);
#else
                        DesktopBackground.DisplayPicture(fi.FullName, true);
#endif
                    }
                }
            }
        }

        private static void LoadSettings()
        {
            try
            {
                string savePath = SettingsFilepath;
                if (File.Exists(savePath))
                {
#if DEBUG
                    bool success = false;
#endif
                    string saved = File.ReadAllText(savePath, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        string[] splitted = saved.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitted != null && splitted.Length == 5)
                        {
                            // Parse data values.
                            switch (splitted[0])
                            {
                                case "Best-Of": SettingTypeFondEcran = 0; break;
                                case "Page d'accueil": SettingTypeFondEcran = 1; break;
                                default: SettingTypeFondEcran = 0; break;
                            };
                            switch (splitted[2])
                            {
                                case "jour(s)": SettingChangeIntervalType = 0; break;
                                case "heure(s)": SettingChangeIntervalType = 1; break;
                                case "minute(s)": SettingChangeIntervalType = 2; break;
                                case "seconde(s)": SettingChangeIntervalType = 3; break;
                                default: SettingChangeIntervalType = 1; break;
                            };
                            switch (splitted[3])
                            {
                                case "True": SettingStartsWithWindows = true; break;
                                case "False": SettingStartsWithWindows = false; break;
                                default: SettingStartsWithWindows = true; break;
                            };
                            switch (splitted[4])
                            {
                                case "Remplir": SettingWallpaperAspectRatio = WallpaperAspect.FILLED; break;
                                case "Ajusté": SettingWallpaperAspectRatio = WallpaperAspect.FITED; break;
                                case "Étiré": SettingWallpaperAspectRatio = WallpaperAspect.STRETCHED; break;
                                case "Centré": SettingWallpaperAspectRatio = WallpaperAspect.CENTERED; break;
                                case "Mosaïque": SettingWallpaperAspectRatio = WallpaperAspect.TILED; break;
                                case "Étendu": SettingWallpaperAspectRatio = WallpaperAspect.EXTENDED; break;
                                default: SettingWallpaperAspectRatio = DesktopBackground.DefaultAspectRatio; break;
                            }
                            SettingChangeInterval = (int.TryParse(splitted[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval) && interval > 0 && interval < 100) ? interval : 1;

                            // Enforce correct values.
                            if (SettingChangeIntervalType == 3 && SettingChangeInterval < MIN_SECONDS)
                                SettingChangeInterval = MIN_SECONDS;

#if DEBUG
                            success = true;
                        }
                    }
                    if (success)
                        Debug.WriteLine("Successfully loaded settings from [" + savePath + "].");
                    else
                        Debug.WriteLine("Failed to load settings from [" + savePath + "] (incorrect data).");
                }
                else
                    Debug.WriteLine("Failed to load settings from [" + savePath + "] (file not found).");
#else
                        }
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not load settings. Exception=[" + ex.ToString() + "].");
            }
        }
    }
}
