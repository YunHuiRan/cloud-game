using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Ludens.Views
{
    public partial class GameEditorView : UserControl
    {
        private GameEntry _game;
        private bool _editing;
        private bool _loading;
        private string _pendingCover;
        private bool _regenerateCover;

        public event EventHandler<GameActionEventArgs> Saved;
        public event EventHandler Cancelled;

        public bool IsOpen { get; private set; }
        public FrameworkElement PanelElement { get { return EditorPanel; } }
        public FrameworkElement ScrimElement { get { return Scrim; } }

        public GameEditorView()
        {
            InitializeComponent();
        }

        public void OpenNew()
        {
            _game = new GameEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                AddedUtc = DateTime.UtcNow
            };
            _editing = false;
            OpenInternal();
        }

        public void OpenNew(string exePath)
        {
            OpenNew();
            if (string.IsNullOrEmpty(exePath)) return;
            ExeBox.Text = exePath;
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
                TitleBox.Text = SuggestTitle(exePath);
            UpdatePreviewLabels();
            UpdateCoverPreview();
            TitleBox.Focus();
            TitleBox.SelectAll();
        }

        public void OpenEdit(GameEntry game)
        {
            if (game == null) return;
            _game = game;
            _editing = true;
            OpenInternal();
        }

        private void OpenInternal()
        {
            _loading = true;
            try
            {
                _pendingCover = null;
                _regenerateCover = false;

                EditorTitle.Text = _editing ? "编辑游戏" : "添加游戏";
                SaveButton.Content = _editing ? "保存修改" : "添加到库";

                TitleBox.Text = _game.Title ?? "";
                ExeBox.Text = _game.ExePath ?? "";

                ErrorText.Visibility = Visibility.Collapsed;
                UpdatePreviewLabels();
                UpdateCoverPreview();

                IsOpen = true;
                Visibility = Visibility.Visible;
            }
            finally
            {
                _loading = false;
            }
            Dispatcher.BeginInvoke(new Action(delegate
            {
                TitleBox.Focus();
                TitleBox.SelectAll();
            }), DispatcherPriority.Input);
        }

        public void CloseImmediate()
        {
            IsOpen = false;
            Visibility = Visibility.Collapsed;
        }

        private void UpdatePreviewLabels()
        {
            string title = TitleBox.Text == null ? "" : TitleBox.Text.Trim();
            if (title.Length == 0) title = "未命名游戏";
            PreviewTitle.Text = title;
            PreviewInitial.Text = title.Substring(0, 1).ToUpperInvariant();

            string exe = ExeBox.Text == null ? "" : ExeBox.Text.Trim();
            try
            {
                PreviewMeta.Text = exe.Length == 0 ? "" : Path.GetFileName(exe).ToUpperInvariant();
            }
            catch
            {
                PreviewMeta.Text = "";
            }
        }

        private void UpdateCoverPreview()
        {
            ImageSource source = null;
            string status;
            bool custom = false;
            if (_pendingCover != null)
            {
                source = LoadAbsolute(_pendingCover);
                status = "新封面 · " + Path.GetFileName(_pendingCover);
                custom = true;
            }
            else if (_regenerateCover)
            {
                status = "保存时自动生成封面";
            }
            else if (!string.IsNullOrEmpty(_game.CoverImage))
            {
                source = MediaService.Load(_game.CoverImage);
                status = _game.CoverGenerated ? "自动生成封面" : Path.GetFileName(_game.CoverImage);
                custom = !_game.CoverGenerated;
            }
            else
            {
                status = "保存时自动生成封面";
            }

            CoverImage.Source = source;
            CoverPlaceholder.Visibility = source == null ? Visibility.Visible : Visibility.Collapsed;
            CoverBadge.Visibility = custom && source != null ? Visibility.Visible : Visibility.Collapsed;
            CoverStatusText.Text = status;
        }

        private static ImageSource LoadAbsolute(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || PreviewTitle == null || PreviewInitial == null) return;
            UpdatePreviewLabels();
        }

        private void ExeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            UpdatePreviewLabels();
        }

        private void BrowseExe_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择游戏可执行文件",
                Filter = "游戏程序|*.exe;*.bat;*.cmd|所有文件|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            ExeBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
                TitleBox.Text = SuggestTitle(dialog.FileName);
            UpdatePreviewLabels();
            UpdateCoverPreview();
            string title = TitleBox.Text.Trim();
            if (title.Length > 0)
            {
                TitleBox.Focus();
                TitleBox.CaretIndex = title.Length;
            }
        }

        private static string SuggestTitle(string path)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrWhiteSpace(info.ProductName) && info.ProductName.Length < 80)
                    return info.ProductName.Trim();
                if (!string.IsNullOrWhiteSpace(info.FileDescription) && info.FileDescription.Length < 80)
                    return info.FileDescription.Trim();
            }
            catch { }
            try
            {
                return Path.GetFileNameWithoutExtension(path);
            }
            catch
            {
                return "未命名游戏";
            }
        }

        private void ChooseCover_Click(object sender, RoutedEventArgs e)
        {
            string path = PickImage("选择游戏封面");
            if (path == null) return;
            _pendingCover = path;
            _regenerateCover = false;
            UpdateCoverPreview();
        }

        private void RegenerateCover_Click(object sender, RoutedEventArgs e)
        {
            _pendingCover = null;
            _regenerateCover = true;
            UpdateCoverPreview();
        }

        private string PickImage(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|所有文件|*.*",
                CheckFileExists = true
            };
            return dialog.ShowDialog(Window.GetWindow(this)) == true ? dialog.FileName : null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string title = (TitleBox.Text ?? "").Trim();
            string exe = (ExeBox.Text ?? "").Trim();
            if (title.Length == 0 && exe.Length > 0) title = SuggestTitle(exe);
            if (title.Length == 0)
            {
                ShowError("请填写游戏名称。");
                TitleBox.Focus();
                return;
            }
            if (exe.Length == 0)
            {
                ShowError("请选择游戏的可执行文件。");
                ExeBox.Focus();
                return;
            }
            if (!File.Exists(exe))
            {
                var result = MessageBox.Show(Window.GetWindow(this),
                    "找不到这个文件：\n" + exe + "\n\n仍然保存吗？之后可以在编辑中修正路径。",
                    "文件不存在", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            for (int i = 0; i < AppStore.Instance.Games.Count; i++)
            {
                var other = AppStore.Instance.Games[i];
                if (other == _game) continue;
                if (!string.IsNullOrEmpty(other.ExePath) && string.Equals(other.ExePath.Trim(), exe, StringComparison.OrdinalIgnoreCase))
                {
                    var result = MessageBox.Show(Window.GetWindow(this),
                        "这个程序已经在游戏库中：" + other.Title + "\n\n仍要保存吗？",
                        "重复的游戏", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result != MessageBoxResult.Yes) return;
                    break;
                }
            }

            try
            {
                bool titleChanged = !string.Equals(_game.Title ?? "", title, StringComparison.Ordinal);
                if (titleChanged && _pendingCover == null && !_regenerateCover && _game.CoverGenerated)
                    _regenerateCover = true;

                _game.Title = title;
                _game.ExePath = exe;
                if (string.IsNullOrWhiteSpace(_game.WorkingDirectory))
                {
                    try { _game.WorkingDirectory = Path.GetDirectoryName(exe) ?? ""; }
                    catch { }
                }

                if (_pendingCover != null)
                {
                    _game.CoverImage = MediaService.ImportGameImage(_game, MediaKind.Cover, _pendingCover);
                    _game.CoverGenerated = false;
                }
                else if (_regenerateCover)
                {
                    _game.CoverImage = "";
                    _game.CoverGenerated = false;
                    MediaService.RegenerateCover(_game);
                }

                MediaService.EnsureArtwork(_game);
                if (_editing)
                {
                    AppStore.Instance.Save();
                }
                else
                {
                    AppStore.Instance.AddGame(_game);
                }

                if (Saved != null) Saved(this, new GameActionEventArgs(_game));
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                ShowError("保存失败：" + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Cancelled != null) Cancelled(this, EventArgs.Empty);
        }

        private void Scrim_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource == Scrim && Cancelled != null) Cancelled(this, EventArgs.Empty);
        }
    }
}
