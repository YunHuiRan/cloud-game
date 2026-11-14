using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ludens.Views
{
    public partial class DetailView : UserControl
    {
        private GameEntry _game;

        public event EventHandler CloseRequested;
        public event EventHandler<GameActionEventArgs> PlayRequested;
        public event EventHandler<GameActionEventArgs> StopRequested;
        public event EventHandler<GameActionEventArgs> EditRequested;
        public event EventHandler<GameActionEventArgs> DeleteRequested;

        public DetailView()
        {
            InitializeComponent();
        }

        public GameEntry Game { get { return _game; } }
        public Border CoverHost { get { return CoverElement; } }
        public ImageSource CoverSource { get { return CoverImage.Source; } }

        public void Open(GameEntry game)
        {
            _game = game;
            DataContext = game;
            Refresh();
            Root.Opacity = 1;
        }

        public void SetCoverVisible(bool visible)
        {
            CoverImage.Opacity = visible ? 1 : 0;
            CoverInitial.Opacity = visible ? 0.35 : 0;
        }

        public void Refresh()
        {
            if (_game == null) return;
            BitmapSource cover = MediaService.Load(_game.CoverImage);
            CoverImage.Source = cover;
            CoverInitial.Visibility = cover == null ? Visibility.Visible : Visibility.Collapsed;
            CoverInitial.Text = _game.Initial;

            BitmapSource logo = MediaService.Load(_game.LogoImage);
            LogoImage.Source = logo;
            LogoImage.Visibility = logo == null ? Visibility.Collapsed : Visibility.Visible;

            SessionsList.ItemsSource = _game.Sessions.Take(8).ToList();
            SessionCountText.Text = _game.Sessions.Count.ToString();
            bool hasSessions = _game.Sessions.Count > 0;
            SessionsList.Visibility = hasSessions ? Visibility.Visible : Visibility.Collapsed;
            EmptySessions.Visibility = hasSessions ? Visibility.Collapsed : Visibility.Visible;

            bool hasDescription = !string.IsNullOrWhiteSpace(_game.Description);
            DescriptionText.Visibility = hasDescription ? Visibility.Visible : Visibility.Collapsed;
            DescriptionText.Text = hasDescription ? _game.Description : string.Empty;
            DescriptionText.Foreground = ThemeManager.Brush("TextSecondary");

            ExeText.Text = string.IsNullOrEmpty(_game.ExePath) ? "未设置" : _game.ExePath;
            ExeText.ToolTip = _game.ExePath;
            WorkDirText.Text = string.IsNullOrEmpty(_game.WorkingDirectory) ? "与可执行文件相同" : _game.WorkingDirectory;
            WorkDirText.ToolTip = _game.WorkingDirectory;
            AddedText.Text = _game.AddedUtc.ToLocalTime().ToString("yyyy年M月d日");
            IdText.Text = _game.Id;

            bool exists = !string.IsNullOrEmpty(_game.ExePath) && File.Exists(_game.ExePath);
            PlayButton.IsEnabled = exists;
            if (!exists) PlayButton.ToolTip = "找不到可执行文件，请先编辑游戏路径";

            UpdateState();
            UpdateFavorite();
        }

        public void UpdateState()
        {
            if (_game == null) return;
            if (_game.IsRunning)
            {
                RunningChip.Visibility = Visibility.Visible;
                PlayGlyph.Text = "\uE71A";
                PlayLabel.Text = "停止游戏";
            }
            else
            {
                RunningChip.Visibility = Visibility.Collapsed;
                PlayGlyph.Text = "\uE768";
                PlayLabel.Text = "开始游戏";
            }
            LastPlayedText.Text = _game.LastPlayedDisplay;
            CoverPlaytime.Text = _game.PlaytimeLongDisplay;
        }

        public void UpdateFavorite()
        {
            if (_game == null) return;
            FavoriteGlyph.Text = _game.Favorite ? "\uE735" : "\uE734";
            FavoriteGlyph.Foreground = ThemeManager.Brush(_game.Favorite ? "Accent" : "TextSecondary");
            FavoriteButton.ToolTip = _game.Favorite ? "取消收藏" : "收藏";
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CloseRequested != null) CloseRequested(this, EventArgs.Empty);
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            if (_game.IsRunning)
            {
                if (StopRequested != null) StopRequested(this, new GameActionEventArgs(_game));
            }
            else
            {
                if (PlayRequested != null) PlayRequested(this, new GameActionEventArgs(_game));
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_game != null && EditRequested != null) EditRequested(this, new GameActionEventArgs(_game));
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_game != null && DeleteRequested != null) DeleteRequested(this, new GameActionEventArgs(_game));
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            AppStore.Instance.ToggleFavorite(_game);
            UpdateFavorite();
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            if (_game == null) return;
            try
            {
                if (!string.IsNullOrEmpty(_game.ExePath) && File.Exists(_game.ExePath))
                    Process.Start("explorer.exe", "/select,\"" + _game.ExePath + "\"");
                else if (!string.IsNullOrEmpty(_game.ExePath))
                    Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(_game.ExePath) + "\"");
            }
            catch (Exception ex) { AppStore.Log(ex); }
        }
    }
}
