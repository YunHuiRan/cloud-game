using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ludens;

namespace Ludens.Views
{
    public partial class StatsView : UserControl
    {
        private static readonly string[] WeekNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

        public int RunningGames { get; set; }

        public StatsView()
        {
            InitializeComponent();
        }

        public void Reload()
        {
            var store = AppStore.Instance;
            var games = store.Games;
            var allSessions = games
                .SelectMany(g => (g.Sessions ?? new List<PlaySession>()).Select(s => new SessionRow
                {
                    Title = g.Title,
                    Start = s.StartUtc.ToLocalTime(),
                    Seconds = s.Seconds,
                    DateText = s.DateDisplay,
                    DurationText = GameEntry.FormatDuration(s.Seconds)
                }))
                .OrderByDescending(s => s.Start)
                .ToList();

            long total = games.Sum(g => g.DisplaySeconds);
            long week = 0;
            long prevWeek = 0;
            var today = DateTime.Now.Date;
            var dayStart = today.AddDays(-6);
            foreach (var g in games)
            {
                foreach (var s in g.Sessions ?? new List<PlaySession>())
                {
                    var local = s.StartUtc.ToLocalTime();
                    if (local >= dayStart) week += s.Seconds;
                    else if (local >= dayStart.AddDays(-7)) prevWeek += s.Seconds;
                }
            }

            TotalTimeText.Text = GameEntry.FormatDuration(total);
            TotalTimeHint.Text = allSessions.Count == 0
                ? "启动游戏后开始累计"
                : allSessions.Count + " 次游玩记录";
            WeekTimeText.Text = GameEntry.FormatDuration(week);
            WeekTimeHint.Text = CompareWeek(week, prevWeek);
            WeekSummaryText.Text = "共 " + GameEntry.FormatDuration(week);

            GameCountText.Text = games.Count + " 款";
            var favorites = games.Count(g => g.Favorite);
            GameCountHint.Text = favorites > 0 ? favorites + " 款已收藏" : "你的私人游戏收藏";

            var top = games.OrderByDescending(g => g.DisplaySeconds).FirstOrDefault(g => g.DisplaySeconds > 0);
            TopGameText.Text = top == null ? "—" : top.Title;
            TopGameHint.Text = top == null ? "还没有记录" : GameEntry.FormatDuration(top.DisplaySeconds) + " · " + top.LaunchCount + " 次启动";

            LiveStatusText.Text = RunningGames > 0 ? RunningGames + " 款正在运行" : "本地数据";
            BuildDays(games, today);
            BuildRecent(allSessions);
            Dispatcher.BeginInvoke(new Action(AnimateBars), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static string CompareWeek(long week, long prev)
        {
            if (prev <= 0 && week <= 0) return "与上周持平";
            if (prev <= 0) return "上周没有记录";
            var diff = week - prev;
            if (Math.Abs(diff) < 60) return "与上周持平";
            var percent = Math.Abs((int)Math.Round(diff * 100.0 / prev));
            return diff > 0 ? "较上周 +" + percent + "%" : "较上周 -" + percent + "%";
        }

        private void BuildDays(List<GameEntry> games, DateTime today)
        {
            var secondsByDay = new long[7];
            long max = 1;
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                long seconds = 0;
                foreach (var g in games)
                {
                    foreach (var s in g.Sessions ?? new List<PlaySession>())
                    {
                        if (s.StartUtc.ToLocalTime().Date == date) seconds += s.Seconds;
                    }
                }
                secondsByDay[i] = seconds;
                if (seconds > max) max = seconds;
            }

            var rows = new List<DayStat>();
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                long seconds = secondsByDay[i];
                rows.Add(new DayStat
                {
                    Label = i == 0 ? "今天" : WeekNames[(int)date.DayOfWeek],
                    ValueText = seconds > 0 ? Compact(seconds) : "—",
                    Seconds = seconds,
                    BarHeight = seconds > 0 ? 14 + (seconds * 112.0 / max) : 4,
                    Tooltip = date.ToString("M月d日") + " · " + (seconds > 0 ? GameEntry.FormatDuration(seconds) : "没有游玩"),
                    LabelBrush = (Brush)Application.Current.TryFindResource(i == 0 ? "Accent" : "TextMuted")
                });
            }
            DaysList.ItemsSource = rows;
        }

        private static string Compact(long seconds)
        {
            if (seconds >= 3600) return (seconds / 3600.0).ToString("0.#") + "h";
            return Math.Max(1, seconds / 60) + "m";
        }

        private void BuildRecent(List<SessionRow> sessions)
        {
            if (sessions.Count == 0)
            {
                EmptyRecent.Visibility = Visibility.Visible;
                RecentList.ItemsSource = null;
                return;
            }
            EmptyRecent.Visibility = Visibility.Collapsed;
            RecentList.ItemsSource = sessions.Take(7).Select(s => new SessionRow
            {
                Title = s.Title,
                Start = s.Start,
                DateText = s.Start.ToString("M月d日 HH:mm"),
                DurationText = GameEntry.FormatDuration(s.Seconds)
            }).ToList();
        }

        private void AnimateBars()
        {
            if (!AppStore.Instance.Settings.Animations) return;
            for (int i = 0; i < DaysList.Items.Count; i++)
            {
                var bar = FindBar(DaysList, i, "Bar");
                if (bar == null) continue;
                ScaleTransform scale = bar.RenderTransform as ScaleTransform;
                if (scale == null || scale.IsFrozen) scale = new ScaleTransform(1, 1);
                bar.RenderTransformOrigin = new Point(0.5, 1);
                bar.RenderTransform = scale;
                scale.ScaleY = 0;
                var animation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(460)))
                {
                    BeginTime = TimeSpan.FromMilliseconds(i * 45),
                    EasingFunction = Motion.EaseOut
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
            }
        }

        private static Border FindBar(ItemsControl items, int index, string name)
        {
            var container = items.ItemContainerGenerator.ContainerFromIndex(index) as DependencyObject;
            if (container == null) return null;
            var found = FindNameInTree(container, name);
            return found as Border;
        }

        private static object FindNameInTree(DependencyObject root, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var fe = child as FrameworkElement;
                if (fe != null && fe.Name == name) return fe;
                var nested = FindNameInTree(child, name);
                if (nested != null) return nested;
            }
            return null;
        }

        public class DayStat
        {
            public string Label { get; set; }
            public string ValueText { get; set; }
            public long Seconds { get; set; }
            public double BarHeight { get; set; }
            public string Tooltip { get; set; }
            public Brush LabelBrush { get; set; }
        }

        public class SessionRow
        {
            public string Title { get; set; }
            public DateTime Start { get; set; }
            public string DateText { get; set; }
            public string DurationText { get; set; }
            public long Seconds { get; set; }
        }
    }
}
