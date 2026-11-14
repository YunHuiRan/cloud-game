using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ludens.Views
{
    public partial class SettingsView : UserControl
    {
        private bool _loading;

        public event EventHandler AppearanceChanged;
        public event EventHandler BackgroundChanged;
        public event EventHandler DimChanged;
        public event EventHandler DataChanged;
        public event Action<string, bool> Notify;

        public SettingsView()
        {
            InitializeComponent();
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                var settings = AppStore.Instance.Settings;
                var store = AppStore.Instance;

                DimSlider.Value = Math.Max(0, Math.Min(0.9, settings.BackgroundDim));
                DimValueText.Text = Math.Round(settings.BackgroundDim * 100) + "%";
                TrayToggle.IsChecked = settings.MinimizeToTray;

                UpdateThemeCards();
                UpdateAccentSelection();
                UpdateBackgroundPreview();

                var sessions = store.Games.Sum(g => g.Sessions == null ? 0 : g.Sessions.Count);
                long bytes = 0;
                try
                {
                    if (File.Exists(AppPaths.DataFile)) bytes = new FileInfo(AppPaths.DataFile).Length;
                }
                catch { }
                DataSummaryText.Text = store.Games.Count + " 款游戏 · " + sessions + " 次游玩 · " + AppPaths.FormatBytes(bytes);
                DataPathText.Text = AppPaths.DataFile;
                VersionText.Text = typeof(SettingsView).Assembly.GetName().Version.ToString(3);
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateThemeCards()
        {
            var settings = AppStore.Instance.Settings;
            bool dark = !string.Equals(settings.Theme, "light", StringComparison.OrdinalIgnoreCase);
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.AccentHex));
            accent.Freeze();
            var border = ThemeManager.Brush("Border");
            DarkCard.BorderBrush = dark ? accent : border;
            DarkCard.BorderThickness = new Thickness(dark ? 2 : 1);
            LightCard.BorderBrush = dark ? border : accent;
            LightCard.BorderThickness = new Thickness(dark ? 1 : 2);
            DarkCheck.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;
            LightCheck.Visibility = dark ? Visibility.Collapsed : Visibility.Visible;
            DarkCheck.Background = accent;
            LightCheck.Background = accent;
        }

        private void UpdateAccentSelection()
        {
            var settings = AppStore.Instance.Settings;
            var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.AccentHex));
            accent.Freeze();
            for (int i = 0; i < AccentPanel.Children.Count; i++)
            {
                var swatch = AccentPanel.Children[i] as Border;
                if (swatch == null) continue;
                bool selected = string.Equals(swatch.Tag as string, settings.AccentHex, StringComparison.OrdinalIgnoreCase);
                swatch.BorderBrush = selected ? accent : Brushes.Transparent;
            }
        }

        private void UpdateBackgroundPreview()
        {
            var settings = AppStore.Instance.Settings;
            var image = MediaService.Load(settings.BackgroundImage);
            BgPreviewImage.Source = image;
            bool has = image != null;
            BgEmptyPanel.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            BgStatusText.Text = has ? "自定义背景" : "默认背景";
            if (DimSlider != null)
            {
                DimSlider.IsEnabled = has;
                DimSlider.Opacity = has ? 1 : 0.35;
            }
            if (DimLabel != null) DimLabel.Opacity = has ? 1 : 0.45;
            if (DimValueText != null) DimValueText.Opacity = has ? 1 : 0.45;
        }

        private void DarkCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetTheme("dark");
        }

        private void LightCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SetTheme("light");
        }

        private void SetTheme(string theme)
        {
            if (_loading) return;
            var settings = AppStore.Instance.Settings;
            if (string.Equals(settings.Theme, theme, StringComparison.OrdinalIgnoreCase)) return;
            settings.Theme = theme;
            AppStore.Instance.Save(false);
            UpdateThemeCards();
            RaiseAppearance();
        }

        private void Accent_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_loading) return;
            var element = sender as FrameworkElement;
            string hex = element == null ? null : element.Tag as string;
            if (string.IsNullOrEmpty(hex)) return;
            var settings = AppStore.Instance.Settings;
            if (string.Equals(settings.AccentHex, hex, StringComparison.OrdinalIgnoreCase)) return;
            settings.AccentHex = hex;
            AppStore.Instance.Save(false);
            UpdateThemeCards();
            UpdateAccentSelection();
            RaiseAppearance();
        }

        private void DimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            var settings = AppStore.Instance.Settings;
            settings.BackgroundDim = e.NewValue;
            DimValueText.Text = Math.Round(e.NewValue * 100) + "%";
            AppStore.Instance.Save(false);
            if (DimChanged != null) DimChanged(this, EventArgs.Empty);
        }

        private void TrayToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            AppStore.Instance.Settings.MinimizeToTray = TrayToggle.IsChecked == true;
            AppStore.Instance.Save(false);
        }

        private void ChooseBackground_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择启动器背景",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp|所有文件|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                var settings = AppStore.Instance.Settings;
                string oldPath = settings.BackgroundImage;
                settings.BackgroundImage = MediaService.ImportAppBackground(dialog.FileName);
                AppStore.Instance.Save(false);
                MediaService.DeleteAppBackground(oldPath);
                UpdateBackgroundPreview();
                if (BackgroundChanged != null) BackgroundChanged(this, EventArgs.Empty);
                RaiseNotify("背景已更新", false);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                RaiseNotify("无法导入背景：" + ex.Message, true);
            }
        }

        private void ClearBackground_Click(object sender, RoutedEventArgs e)
        {
            var settings = AppStore.Instance.Settings;
            if (string.IsNullOrEmpty(settings.BackgroundImage)) return;
            string oldPath = settings.BackgroundImage;
            settings.BackgroundImage = "";
            AppStore.Instance.Save(false);
            MediaService.DeleteAppBackground(oldPath);
            UpdateBackgroundPreview();
            if (BackgroundChanged != null) BackgroundChanged(this, EventArgs.Empty);
            RaiseNotify("已恢复默认背景", false);
        }

        private void ExportPlay_Click(object sender, RoutedEventArgs e)
        {
            Export(false);
        }

        private void ExportFull_Click(object sender, RoutedEventArgs e)
        {
            Export(true);
        }

        private void Export(bool includeMedia)
        {
            var dialog = new SaveFileDialog
            {
                Title = includeMedia ? "导出完整备份" : "导出游玩数据",
                Filter = "Cloud Game 数据 (*.json)|*.json|所有文件|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "cloudgame-" + (includeMedia ? "backup-" : "play-") + DateTime.Now.ToString("yyyyMMdd") + ".json"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                long size = AppStore.Instance.ExportToFile(dialog.FileName, includeMedia);
                RaiseNotify("已导出 " + AppPaths.FormatBytes(size) + " 到 " + Path.GetFileName(dialog.FileName), false);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                RaiseNotify("导出失败：" + ex.Message, true);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入 Cloud Game 数据",
                Filter = "Cloud Game 数据 (*.json)|*.json|所有文件|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            var owner = Window.GetWindow(this);
            var confirm = MessageBox.Show(owner,
                "导入会合并游戏和游玩记录，并根据 ID、可执行文件路径或名称匹配已有游戏。\n\n继续导入？",
                "导入数据", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            try
            {
                var result = AppStore.Instance.ImportFromFile(dialog.FileName);
                Reload();
                if (DataChanged != null) DataChanged(this, EventArgs.Empty);
                string message = "导入完成：新增 " + result.Added + " 款，合并 " + result.Merged + " 款";
                if (result.Sessions > 0) message += "，恢复 " + result.Sessions + " 次记录";
                if (result.Skipped > 0) message += "，跳过 " + result.Skipped + " 条";
                RaiseNotify(message, false);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                RaiseNotify("导入失败：" + ex.Message, true);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", AppPaths.DataRoot);
            }
            catch (Exception ex)
            {
                RaiseNotify("无法打开目录：" + ex.Message, true);
            }
        }

        private void RaiseAppearance()
        {
            if (AppearanceChanged != null) AppearanceChanged(this, EventArgs.Empty);
        }

        private void RaiseNotify(string message, bool error)
        {
            if (Notify != null) Notify(message, error);
        }
    }
}
