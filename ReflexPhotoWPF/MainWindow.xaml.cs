/*******************************************************/
/* Copyright ReflexPhoto © 2020 - Tous droits réservés */
/* https://reflexphoto.eu <dev@reflexphoto.eu>         */
/*******************************************************/
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ReflexPhotoWPF
{
    public partial class MainWindow : Window
    {
        private int InitTypeFondEcran = 0;
        private int InitChangeInterval = 1;
        private int InitChangeIntervalType = 1;
        private bool InitStartsWithWindows = true;
        private WallpaperAspect InitWallpaperAspect = DesktopBackground.DefaultAspectRatio;
        private bool SettingsLoaded = false;

        public MainWindow()
        {
            Initialized += MainWindow_Initialized;
            InitializeComponent();
        }

        private void MainWindow_Initialized(object sender, EventArgs e)
        {
            // Set initial view state.
            lbl_ErrorMsg.Visibility = Visibility.Collapsed;
            btn_SaveSettings.Visibility = Visibility.Collapsed;
            double left = (SystemParameters.PrimaryScreenWidth / 2) - 250;
            double top = (SystemParameters.PrimaryScreenHeight / 2) - 100;
            this.Left = left > 0 ? left : 0;
            this.Top = top > 0 ? top : 0;
            // Load settings.
            LoadSettings();
        }

        private void SaveSettings()
        {
            try
            {
                string typeFondEcran = ((System.Windows.Controls.ComboBoxItem)cbb_TypeFondEcran.SelectedValue).Content.ToString();
                string changeInterval = tb_ChangeInterval.Text;
                string changeIntervalType = ((System.Windows.Controls.ComboBoxItem)cbb_ChangeIntervalType.SelectedValue).Content.ToString();
                string startWithWindows = (cb_StartWithWindows.IsChecked != null && cb_StartWithWindows.IsChecked.HasValue && cb_StartWithWindows.IsChecked.Value) ? "True" : "False";
                string aspectRatio = ((System.Windows.Controls.ComboBoxItem)cbb_AspectRatioType.SelectedValue).Content.ToString();

                string toSave = typeFondEcran + Environment.NewLine + changeInterval + Environment.NewLine + changeIntervalType + Environment.NewLine + startWithWindows + Environment.NewLine + aspectRatio;
                string savePath = App.SettingsFilepath;

                Debug.WriteLine("Saving settings [" + toSave.Replace(Environment.NewLine, "/") + "] to [" + savePath + "].");
                File.WriteAllText(savePath, toSave, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Could not save settings. Exception=[" + ex.ToString() + "].");
            }
        }

        private void LoadSettings()
        {
            try
            {
#if DEBUG
                bool success = false;
#endif
                string savePath = App.SettingsFilepath;
                if (File.Exists(savePath))
                {
                    string saved = File.ReadAllText(savePath, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        string[] splitted = saved.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        if (splitted != null && splitted.Length == 5)
                        {
                            // Parse data values.
                            switch (splitted[0])
                            {
                                case "Best-Of": cbb_TypeFondEcran.SelectedIndex = 0; break;
                                case "Page d'accueil": cbb_TypeFondEcran.SelectedIndex = 1; break;
                                default: cbb_TypeFondEcran.SelectedIndex = 0; break;
                            };
                            InitTypeFondEcran = cbb_TypeFondEcran.SelectedIndex;
                            if (int.TryParse(splitted[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval))
                            {
                                if (interval < 1 || interval > 99)
                                    interval = 1;
                                tb_ChangeInterval.Text = interval.ToString(CultureInfo.InvariantCulture);
                                InitChangeInterval = interval;
                            }
                            switch (splitted[2])
                            {
                                case "jour(s)": cbb_ChangeIntervalType.SelectedIndex = 0; break;
                                case "heure(s)": cbb_ChangeIntervalType.SelectedIndex = 1; break;
                                case "minute(s)": cbb_ChangeIntervalType.SelectedIndex = 2; break;
                                case "seconde(s)": cbb_ChangeIntervalType.SelectedIndex = 3; break;
                                default: cbb_ChangeIntervalType.SelectedIndex = 1; break;
                            };
                            InitChangeIntervalType = cbb_ChangeIntervalType.SelectedIndex;
                            switch (splitted[3])
                            {
                                case "True": cb_StartWithWindows.IsChecked = true; break;
                                case "False": cb_StartWithWindows.IsChecked = false; break;
                                default: cb_StartWithWindows.IsChecked = true; break;
                            };
                            InitStartsWithWindows = cb_StartWithWindows.IsChecked != null && cb_StartWithWindows.IsChecked.HasValue && cb_StartWithWindows.IsChecked.Value;
                            switch (splitted[4])
                            {
                                case "Remplir": cbb_AspectRatioType.SelectedIndex = 0; break;
                                case "Ajusté": cbb_AspectRatioType.SelectedIndex = 1; break;
                                case "Étiré": cbb_AspectRatioType.SelectedIndex = 2; break;
                                case "Centré": cbb_AspectRatioType.SelectedIndex = 3; break;
                                case "Mosaïque": cbb_AspectRatioType.SelectedIndex = 4; break;
                                case "Étendu": cbb_AspectRatioType.SelectedIndex = 5; break;
                                default: cbb_AspectRatioType.SelectedIndex = (int)DesktopBackground.DefaultAspectRatio; break;
                            }
                            InitWallpaperAspect = (WallpaperAspect)cbb_AspectRatioType.SelectedIndex;

                            // Enforce correct values.
                            if (InitChangeIntervalType == 3)
                            {
                                if (InitChangeInterval < App.MIN_SECONDS)
                                {
                                    InitChangeInterval = App.MIN_SECONDS;
                                    tb_ChangeInterval.Text = InitChangeInterval.ToString(CultureInfo.InvariantCulture);
                                }
                            }
                            else if (InitChangeInterval < 1)
                            {
                                InitChangeInterval = 1;
                                tb_ChangeInterval.Text = "1";
                            }
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
            SettingsLoaded = true;
        }

        private void Btn_SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            bool error = false;
            int interval = (int.TryParse(tb_ChangeInterval.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tbVal) && tbVal >= 0 && tbVal <= 99) ? tbVal : 1;
            if (interval == 0)
            {
                lbl_ErrorMsg.Text = "Impossible de sauvegarder :\nL'interval d'actualisation doit être supérieur à " + (App.MIN_SECONDS - 1).ToString(CultureInfo.InvariantCulture) + " secondes.";
                lbl_ErrorMsg.Visibility = Visibility.Visible;
                error = true;
            }
            else
            {
                if (cbb_ChangeIntervalType.SelectedIndex == 3 && interval < App.MIN_SECONDS)
                {
                    lbl_ErrorMsg.Text = "Impossible de sauvegarder :\nL'interval d'actualisation doit être supérieur à " + (App.MIN_SECONDS - 1).ToString(CultureInfo.InvariantCulture) + " secondes.";
                    lbl_ErrorMsg.Visibility = Visibility.Visible;
                    error = true;
                }
            }

            if (!error)
            {
                if (cb_StartWithWindows.IsChecked != null && cb_StartWithWindows.IsChecked.HasValue)
                {
                    if (cb_StartWithWindows.IsChecked.Value != InitStartsWithWindows)
                    {
                        bool success = true;
                        if (cb_StartWithWindows.IsChecked.Value)
                        {
                            if (!StartWithWindows.SetStartWithWindows())
                                success = false;
                        }
                        else
                        {
                            if (!StartWithWindows.UnsetStartWithWindows())
                                success = false;
                        }
                        if (!success)
                            cb_StartWithWindows.IsChecked = !cb_StartWithWindows.IsChecked.Value;
                    }
                }
                if (cbb_AspectRatioType.SelectedIndex != (int)InitWallpaperAspect)
                    DesktopBackground.SetWallpaperAspectRatio((WallpaperAspect)cbb_AspectRatioType.SelectedIndex);
                SaveSettings();

                InitTypeFondEcran = cbb_TypeFondEcran.SelectedIndex;
                InitChangeInterval = interval;
                InitChangeIntervalType = cbb_ChangeIntervalType.SelectedIndex;
                InitStartsWithWindows = cb_StartWithWindows.IsChecked != null && cb_StartWithWindows.IsChecked.HasValue && cb_StartWithWindows.IsChecked.Value;
                InitWallpaperAspect = (WallpaperAspect)cbb_AspectRatioType.SelectedIndex;
                UpdateSaveSettingsVisibility();

                App.RestartLoop();
            }
        }

        private bool SettingsChanged()
        {
            if (SettingsLoaded)
            {
                if (InitTypeFondEcran != cbb_TypeFondEcran.SelectedIndex)
                    return true;
                if (InitChangeIntervalType != cbb_ChangeIntervalType.SelectedIndex)
                    return true;
                if (int.TryParse(tb_ChangeInterval.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int interval) && interval >= 0 && interval <= 99 && InitChangeInterval != interval)
                    return true;
                if (InitStartsWithWindows != (cb_StartWithWindows.IsChecked != null && cb_StartWithWindows.IsChecked.HasValue && cb_StartWithWindows.IsChecked.Value))
                    return true;
                if (InitWallpaperAspect != (WallpaperAspect)cbb_AspectRatioType.SelectedIndex)
                    return true;
            }
            return false;
        }

        private void UpdateSaveSettingsVisibility()
        {
            if (SettingsLoaded)
            {
                lbl_ErrorMsg.Visibility = Visibility.Collapsed;
                btn_SaveSettings.Visibility = SettingsChanged() ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex _regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
        private static bool IsTextAllowed(string text) => !_regex.IsMatch(text);
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !IsTextAllowed(e.Text);
        private void TextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsTextAllowed(text))
                    e.CancelCommand();
            }
            else
                e.CancelCommand();
        }

        private void Cbb_ChangeIntervalType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateSaveSettingsVisibility();

        private void Tb_ChangeInterval_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateSaveSettingsVisibility();

        private void Cbb_TypeFondEcran_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateSaveSettingsVisibility();

        private void Cb_StartWithWindows_Checked(object sender, RoutedEventArgs e) => UpdateSaveSettingsVisibility();

        private void Cb_StartWithWindows_Unchecked(object sender, RoutedEventArgs e) => UpdateSaveSettingsVisibility();

        private void cbb_AspectRatioType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cbb_AspectRatioType.SelectedIndex == 5)
            {
                MessageBoxResult res = MessageBox.Show("L'aspect étendu ne fonctionne que si plusieurs écrans sont connectés à l'ordinateur.\nÊtes-vous sûr de vouloir utiliser le mode étendu ?", "Attention", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No, MessageBoxOptions.ServiceNotification);
                if (res != MessageBoxResult.Yes)
                {
                    bool found = false;
                    if (e?.RemovedItems != null && e.RemovedItems.Count > 0)
                    {
                        System.Windows.Controls.ComboBoxItem old = (System.Windows.Controls.ComboBoxItem)e.RemovedItems[0];
                        if (old != null)
                        {
                            string str = (string)old.Content;
                            switch (str)
                            {
                                case "Remplir": cbb_AspectRatioType.SelectedIndex = 0; found = true; break;
                                case "Ajusté": cbb_AspectRatioType.SelectedIndex = 1; found = true; break;
                                case "Étiré": cbb_AspectRatioType.SelectedIndex = 2; found = true; break;
                                case "Centré": cbb_AspectRatioType.SelectedIndex = 3; found = true; break;
                                case "Mosaïque": cbb_AspectRatioType.SelectedIndex = 4; found = true; break;
                                case "Étendu": cbb_AspectRatioType.SelectedIndex = 5; found = true; break;
                            }
                        }
                    }
                    if (!found) // Fallback to default aspect ratio.
                        cbb_AspectRatioType.SelectedIndex = (int)DesktopBackground.DefaultAspectRatio;
                }
            }
            UpdateSaveSettingsVisibility();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) { this.DragMove(); } }

        private void TextBlock_PreviewMouseDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount >= 1) { this.Close(); } }
    }
}
