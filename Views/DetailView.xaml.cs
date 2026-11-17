using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ludens.Views
{
    public sealed class CalendarDay
    {
        public int DayNumber { get; set; }
        public double CellOpacity { get; set; }
        public Brush Fill { get; set; }
        public Brush BorderBrush { get; set; }
        public Brush TextBrush { get; set; }
        public string Tooltip { get; set; }
    }

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
            IsVisibleChanged += delegate
            {
                if (IsVisible && _game != null) BuildCalendar();
            };
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
            BuildCalendar();
        }

        private void BuildCalendar()
        {
            if (_game == null || CalendarDays == null) return;

            Dictionary<DateTime, long> totals = new Dictionary<DateTime, long>();
            if (_game.Sessions != null)
            {
                for (int i = 0; i < _game.Sessions.Count; i++)
                {
                    PlaySession session = _game.Sessions[i];
                    if (session == null) continue;
                    AddSessionSeconds(totals, session.StartUtc, session.EndUtc, session.Seconds);
                }
            }
            if (_game.IsRunning && _game.ActiveSessionStartUtc.HasValue)
            {
                AddSessionSeconds(totals, _game.ActiveSessionStartUtc.Value, DateTime.UtcNow, 0);
            }

            DateTime today = DateTime.Today;
            DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            DateTime start = monday.AddDays(-77);
            List<CalendarDay> days = new List<CalendarDay>(84);
            long totalSeconds = 0;

            for (int i = 0; i < 84; i++)
            {
                DateTime date = start.AddDays(i);
                long seconds;
                totals.TryGetValue(date, out seconds);
                if (seconds < 0) seconds = 0;
                totalSeconds += seconds;

                bool future = date > today;
                double intensity = future ? 0 : Intensity(seconds);
                Color accent = ThemeManager.AccentColor;
                Brush fill = intensity <= 0
                    ? Brushes.Transparent
                    : new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * intensity), accent.R, accent.G, accent.B));
                Brush text = intensity >= 0.52
                    ? ThemeManager.Brush("AccentForeground")
                    : ThemeManager.Brush("TextMuted");
                Brush border = date == today
                    ? ThemeManager.Brush("Accent")
                    : ThemeManager.Brush("Border");

                string label = date.ToString("yyyy年M月d日 dddd");
                string detail;
                if (future) detail = "尚未到来";
                else if (seconds <= 0) detail = "无游玩记录";
                else detail = "游玩 " + GameEntry.FormatDuration(seconds);

                days.Add(new CalendarDay
                {
                    DayNumber = date.Day,
                    CellOpacity = future ? 0.32 : 1.0,
                    Fill = fill,
                    BorderBrush = border,
                    TextBrush = text,
                    Tooltip = label + "\n" + detail
                });
            }

            CalendarDays.ItemsSource = days;
            CalendarRangeText.Text = start.ToString("yyyy年M月d日") + " — " + monday.AddDays(6).ToString("M月d日");
            CalendarSummaryText.Text = "最近 12 周 · 共 " + GameEntry.FormatDuration(totalSeconds);
        }

        private static double Intensity(long seconds)
        {
            if (seconds <= 0) return 0;
            if (seconds < 15 * 60) return 0.18;
            if (seconds < 30 * 60) return 0.34;
            if (seconds < 60 * 60) return 0.52;
            if (seconds < 2 * 60 * 60) return 0.74;
            return 1.0;
        }

        private static void AddSessionSeconds(Dictionary<DateTime, long> totals, DateTime startUtc, DateTime endUtc, long seconds)
        {
            DateTime start = startUtc.ToLocalTime();
            DateTime end = endUtc.ToLocalTime();
            if (end < start) end = start;
            if (end == start)
            {
                if (seconds > 0) AddSeconds(totals, start.Date, seconds);
                return;
            }

            double wallSeconds = (end - start).TotalSeconds;
            long sourceSeconds = seconds > 0 ? seconds : (long)Math.Round(wallSeconds);
            DateTime cursor = start.Date;
            while (cursor <= end.Date)
            {
                DateTime dayEnd = cursor.AddDays(1);
                DateTime overlapStart = start > cursor ? start : cursor;
                DateTime overlapEnd = end < dayEnd ? end : dayEnd;
                if (overlapEnd > overlapStart)
                {
                    double fraction = (overlapEnd - overlapStart).TotalSeconds / wallSeconds;
                    long add = (long)Math.Round(sourceSeconds * fraction);
                    if (add > 0) AddSeconds(totals, cursor, add);
                }
                cursor = dayEnd;
            }
        }

        private static void AddSeconds(Dictionary<DateTime, long> totals, DateTime date, long seconds)
        {
            long current;
            totals.TryGetValue(date, out current);
            totals[date] = current + seconds;
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
