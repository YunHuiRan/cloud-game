using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Ludens.Views
{
    public partial class LibraryView : UserControl
    {
        private readonly AppStore _store;
        private readonly DispatcherTimer _searchTimer;
        private Dictionary<string, Rect> _oldRects = new Dictionary<string, Rect>();
        private int _filter;
        private bool _suppressViewEvents;
        private bool _loaded;
        private bool _firstStagger = true;
        private bool _searchExpanded;
        private string _viewMode = "grid";

        public event EventHandler AddGameRequested;
        public event EventHandler<GameOpenEventArgs> GameOpenRequested;
        public event EventHandler<GameActionEventArgs> PlayRequested;
        public event EventHandler<GameActionEventArgs> StopRequested;
        public event EventHandler<GameActionEventArgs> EditRequested;
        public event EventHandler<GameActionEventArgs> DeleteRequested;

        public LibraryView()
        {
            InitializeComponent();
            _store = AppStore.Instance;
            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(190);
            _searchTimer.Tick += delegate
            {
                _searchTimer.Stop();
                AnimateContentReload();
            };
            Loaded += delegate
            {
                if (_loaded) return;
                _loaded = true;
                ApplySettings();
                Reload(_firstStagger);
                _firstStagger = false;
            };
        }

        public AppStore Store { get { return _store; } }

        private void ApplySettings()
        {
            _viewMode = _store.Settings.ViewMode == "list" ? "list" : "grid";
            _suppressViewEvents = true;
            GridViewButton.IsChecked = _viewMode == "grid";
            ListViewButton.IsChecked = _viewMode == "list";
            _suppressViewEvents = false;
            AnimateViewThumb(false);

            string sort = _store.Settings.SortMode;
            if (sort == "title") SortLabel.Text = "游戏名称";
            else if (sort == "playtime") SortLabel.Text = "游玩时长";
            else if (sort == "added") SortLabel.Text = "添加时间";
            else SortLabel.Text = "最近游玩";
        }

        public void Reload(bool animate)
        {
            if (!_loaded) return;
            _oldRects = CaptureRects();
            List<GameEntry> games = _store.GetSorted(_filter, SearchBox.Text).ToList();
            GameGrid.ItemsSource = games;
            GameList.ItemsSource = games;

            bool grid = _viewMode != "list";
            GameGrid.Visibility = grid && games.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            GameList.Visibility = !grid && games.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyTitle.Text = string.IsNullOrWhiteSpace(SearchBox.Text) && _filter == 0
                ? "游戏库还是空的"
                : "没有找到匹配的游戏";
            EmptyHint.Text = string.IsNullOrWhiteSpace(SearchBox.Text) && _filter == 0
                ? "添加一个本地游戏，Cloud Game 会自动记录你的游玩时间。"
                : "试试换个关键词或筛选条件。";

            int running = _store.Games.Count(g => g.IsRunning);
            RunningBadge.Visibility = running > 0 ? Visibility.Visible : Visibility.Collapsed;
            RunningBadgeText.Text = running + " 个游戏运行中";
            CountText.Text = _store.Games.Count + " 款游戏  ·  累计 " + GameEntry.FormatDuration(_store.TotalPlaySeconds);
            ResultText.Text = games.Count == _store.Games.Count
                ? "共 " + games.Count + " 款"
                : "找到 " + games.Count + " 款";

            ScheduleReorder(animate);
        }

        public void FocusSearch()
        {
            if (!_searchExpanded) ExpandSearch(false);
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        public void RefreshLive()
        {
            int running = _store.Games.Count(g => g.IsRunning);
            RunningBadge.Visibility = running > 0 ? Visibility.Visible : Visibility.Collapsed;
            RunningBadgeText.Text = running + " 个游戏运行中";
        }

        private Dictionary<string, Rect> CaptureRects()
        {
            Dictionary<string, Rect> rects = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            ItemsControl control = _viewMode == "list" ? GameList : GameGrid;
            if (control == null || control.ItemsSource == null) return rects;
            foreach (object item in control.Items)
            {
                GameEntry game = item as GameEntry;
                if (game == null) continue;
                ContentPresenter presenter = control.ItemContainerGenerator.ContainerFromItem(game) as ContentPresenter;
                if (presenter == null) continue;
                Rect rect = Motion.ScreenRect(presenter);
                if (!rect.IsEmpty) rects[game.Id] = rect;
            }
            return rects;
        }

        private FrameworkElement GetCoverElement(string gameId)
        {
            if (string.IsNullOrEmpty(gameId)) return null;
            GameEntry game = _store.FindById(gameId);
            if (game == null) return null;
            ItemsControl control = _viewMode == "list" ? GameList : GameGrid;
            if (control == null) return null;
            control.UpdateLayout();
            ContentPresenter presenter = control.ItemContainerGenerator.ContainerFromItem(game) as ContentPresenter;
            if (presenter == null)
            {
                control.UpdateLayout();
                presenter = control.ItemContainerGenerator.ContainerFromItem(game) as ContentPresenter;
            }
            if (presenter == null) return null;
            FrameworkElement cover = FindByName(presenter, _viewMode == "list" ? "RowCover" : "CoverFrame");
            return cover ?? presenter;
        }

        public Rect GetCoverRect(string gameId)
        {
            FrameworkElement cover = GetCoverElement(gameId);
            return cover == null ? Rect.Empty : Motion.ScreenRect(cover);
        }

        public void SetCoverOpacity(string gameId, double opacity, bool animate)
        {
            FrameworkElement cover = GetCoverElement(gameId);
            if (cover == null) return;
            cover.BeginAnimation(OpacityProperty, null);
            if (!animate || _store.Settings == null || !_store.Settings.Animations)
            {
                cover.Opacity = opacity;
                return;
            }
            // Start from zero without touching the base value: setting Opacity=1
            // before BeginAnimation can render one bright frame and look like a flash.
            cover.Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0, opacity, TimeSpan.FromMilliseconds(240));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            fade.FillBehavior = FillBehavior.HoldEnd;
            cover.BeginAnimation(OpacityProperty, fade);
        }

        private void ScheduleReorder(bool animate)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
            {
                UpdateLayout();
                ItemsControl control = _viewMode == "list" ? GameList : GameGrid;
                if (control == null || control.ItemsSource == null) return;
                int index = 0;
                foreach (object item in control.Items)
                {
                    GameEntry game = item as GameEntry;
                    if (game == null) continue;
                    ContentPresenter presenter = control.ItemContainerGenerator.ContainerFromItem(game) as ContentPresenter;
                    if (presenter == null) continue;
                    Rect old;
                    if (_oldRects.TryGetValue(game.Id, out old) && !old.IsEmpty)
                    {
                        if (animate) Motion.Flip(presenter, old, FlipRoot, null);
                    }
                    else if (animate)
                    {
                        Motion.SlideFadeIn(presenter, 0, 14, Math.Min(index, 10) * 32, 300);
                    }
                    index++;
                }
            }));
        }

        private static FrameworkElement FindByName(DependencyObject root, string name)
        {
            if (root == null) return null;
            FrameworkElement element = root as FrameworkElement;
            if (element != null && element.Name == name) return element;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                FrameworkElement found = FindByName(VisualTreeHelper.GetChild(root, i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void AddGame_Click(object sender, RoutedEventArgs e)
        {
            if (AddGameRequested != null) AddGameRequested(this, EventArgs.Empty);
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            GameEntry game = button.DataContext as GameEntry;
            if (game == null) return;
            FrameworkElement source = FindByName(button, "CoverFrame") ?? FindByName(button, "RowCover") ?? button;
            Rect rect = Motion.ScreenRect(source);
            if (GameOpenRequested != null) GameOpenRequested(this, new GameOpenEventArgs(game, rect));
        }

        private void CardPlay_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            FrameworkElement source = sender as FrameworkElement;
            GameEntry game = source == null ? null : source.DataContext as GameEntry;
            if (game == null) return;
            if (game.IsRunning)
            {
                if (StopRequested != null) StopRequested(this, new GameActionEventArgs(game));
            }
            else if (PlayRequested != null)
            {
                PlayRequested(this, new GameActionEventArgs(game));
            }
        }

        private void GameRow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement row = sender as FrameworkElement;
            GameEntry game = row == null ? null : row.DataContext as GameEntry;
            if (game == null) return;
            Point point = e.GetPosition(row);
            if (point.X < row.ActualWidth - 70) return;
            e.Handled = true;
            if (game.IsRunning)
            {
                if (StopRequested != null) StopRequested(this, new GameActionEventArgs(game));
            }
            else if (PlayRequested != null)
            {
                PlayRequested(this, new GameActionEventArgs(game));
            }
        }

        private void PlayMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game != null && PlayRequested != null) PlayRequested(this, new GameActionEventArgs(game));
        }

        private void StopMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game != null && StopRequested != null) StopRequested(this, new GameActionEventArgs(game));
        }

        private void DetailMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game != null && GameOpenRequested != null) GameOpenRequested(this, new GameOpenEventArgs(game, Rect.Empty));
        }

        private void EditMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game != null && EditRequested != null) EditRequested(this, new GameActionEventArgs(game));
        }

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game != null && DeleteRequested != null) DeleteRequested(this, new GameActionEventArgs(game));
        }

        private void FavoriteMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game == null) return;
            _store.ToggleFavorite(game);
            AnimateContentReload();
        }

        private void RevealMenu_Click(object sender, RoutedEventArgs e)
        {
            GameEntry game = DataContextOf(sender);
            if (game == null) return;
            try
            {
                string path = game.ExePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                }
                else if (!string.IsNullOrEmpty(path) && Directory.Exists(Path.GetDirectoryName(path)))
                {
                    Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(path) + "\"");
                }
            }
            catch (Exception ex) { AppStore.Log(ex); }
        }

        private static GameEntry DataContextOf(object sender)
        {
            MenuItem item = sender as MenuItem;
            return item == null ? null : item.DataContext as GameEntry;
        }

        private void SearchToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_searchExpanded)
            {
                ExpandSearch(true);
                return;
            }
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = "";
                SearchBox.Focus();
                return;
            }
            CollapseSearch();
        }

        private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_searchExpanded) return;
            DependencyObject node = e.OriginalSource as DependencyObject;
            while (node != null)
            {
                if (node == SearchShell) return;
                node = VisualTreeHelper.GetParent(node);
            }
            CollapseSearch();
        }

        private void SearchShell_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_searchExpanded) return;
                if (SearchShell.IsKeyboardFocusWithin) return;
                if (!string.IsNullOrEmpty(SearchBox.Text)) return;
                CollapseSearch();
            }), DispatcherPriority.Input);
        }

        private void ExpandSearch(bool animate)
        {
            _searchExpanded = true;
            if (!animate)
            {
                SearchShell.Width = 282;
                SearchBox.Width = 206;
                SearchBox.Opacity = 1;
                return;
            }
            DoubleAnimation width = new DoubleAnimation(SearchShell.ActualWidth, 282, TimeSpan.FromMilliseconds(240));
            width.EasingFunction = Motion.EaseOut;
            SearchShell.BeginAnimation(WidthProperty, width);
            DoubleAnimation textWidth = new DoubleAnimation(0, 206, TimeSpan.FromMilliseconds(240));
            textWidth.EasingFunction = Motion.EaseOut;
            SearchBox.BeginAnimation(WidthProperty, textWidth);
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
            fade.BeginTime = TimeSpan.FromMilliseconds(60);
            SearchBox.BeginAnimation(OpacityProperty, fade);
            SearchBox.Focus();
        }

        private void CollapseSearch()
        {
            _searchExpanded = false;
            DoubleAnimation width = new DoubleAnimation(SearchShell.ActualWidth, 44, TimeSpan.FromMilliseconds(190));
            width.EasingFunction = Motion.EaseInOut;
            SearchShell.BeginAnimation(WidthProperty, width);
            DoubleAnimation textWidth = new DoubleAnimation(SearchBox.ActualWidth, 0, TimeSpan.FromMilliseconds(190));
            textWidth.EasingFunction = Motion.EaseInOut;
            SearchBox.BeginAnimation(WidthProperty, textWidth);
            DoubleAnimation fade = new DoubleAnimation(SearchBox.Opacity, 0, TimeSpan.FromMilliseconds(120));
            SearchBox.BeginAnimation(OpacityProperty, fade);
            ClearSearchButton.Visibility = Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ClearSearchButton.Visibility = _searchExpanded && !string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (!_loaded) return;
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radio = sender as RadioButton;
            if (radio == null) return;
            int value;
            if (!int.TryParse(radio.Tag as string, out value)) return;
            if (_filter == value)
            {
                if (_loaded) AnimateFilterButton(radio);
                return;
            }
            _filter = value;
            if (!_loaded) return;
            AnimateFilterButton(radio);
            AnimateContentReload();
        }

        private void AnimateFilterButton(RadioButton radio)
        {
            if (radio == null || _store == null || !_store.Settings.Animations) return;
            var fade = new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(190));
            fade.EasingFunction = Motion.EaseOut;
            radio.BeginAnimation(OpacityProperty, fade);
        }

        private void AnimateContentReload()
        {
            if (!_store.Settings.Animations)
            {
                Reload(false);
                return;
            }
            var fade = new DoubleAnimation(CollectionHost.Opacity, 0.35, TimeSpan.FromMilliseconds(85));
            fade.EasingFunction = Motion.EaseIn;
            var emptyFade = new DoubleAnimation(EmptyState.Opacity, 0, TimeSpan.FromMilliseconds(85));
            emptyFade.EasingFunction = Motion.EaseIn;
            fade.Completed += delegate
            {
                Reload(false);
                var show = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(230));
                show.EasingFunction = Motion.EaseOut;
                CollectionHost.BeginAnimation(OpacityProperty, show);
                if (EmptyState.Visibility == Visibility.Visible)
                {
                    EmptyState.BeginAnimation(OpacityProperty, null);
                    EmptyState.Opacity = 1;
                    var showEmpty = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(230));
                    showEmpty.EasingFunction = Motion.EaseOut;
                    showEmpty.FillBehavior = FillBehavior.HoldEnd;
                    EmptyState.BeginAnimation(OpacityProperty, showEmpty);
                }
            };
            CollectionHost.BeginAnimation(OpacityProperty, fade);
            EmptyState.BeginAnimation(OpacityProperty, emptyFade);
        }

        private void AnimateViewThumb(bool animate)
        {
            if (ViewThumbShift == null) return;
            double target = _viewMode == "list" ? 38 : 0;
            double from = ViewThumbShift.X;
            ViewThumbShift.BeginAnimation(TranslateTransform.XProperty, null);
            ViewThumbShift.X = target;
            if (!animate || _store.Settings == null || !_store.Settings.Animations) return;
            var slide = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(240));
            slide.EasingFunction = Motion.EaseOut;
            slide.FillBehavior = FillBehavior.HoldEnd;
            ViewThumbShift.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private void AnimateSortChevron(double angle)
        {
            if (SortChevronRotate == null) return;
            double from = SortChevronRotate.Angle;
            SortChevronRotate.BeginAnimation(RotateTransform.AngleProperty, null);
            SortChevronRotate.Angle = angle;
            if (_store.Settings == null || !_store.Settings.Animations) return;
            var turn = new DoubleAnimation(from, angle, TimeSpan.FromMilliseconds(180));
            turn.EasingFunction = Motion.EaseOut;
            turn.FillBehavior = FillBehavior.HoldEnd;
            SortChevronRotate.BeginAnimation(RotateTransform.AngleProperty, turn);
        }

        private void ViewMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressViewEvents) return;
            RadioButton radio = sender as RadioButton;
            if (radio == null) return;
            _viewMode = radio == ListViewButton ? "list" : "grid";
            _store.Settings.ViewMode = _viewMode;
            _store.Save(false);
            if (_loaded)
            {
                _oldRects.Clear();
                AnimateViewThumb(true);
                AnimateContentReload();
            }
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            if (SortButton.ContextMenu == null) return;
            SortButton.ContextMenu.PlacementTarget = SortButton;
            SortButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            SortButton.ContextMenu.VerticalOffset = 8;
            SortButton.ContextMenu.IsOpen = true;
        }

        private void SortMenu_Opened(object sender, RoutedEventArgs e)
        {
            AnimateSortChevron(180);
        }

        private void SortMenu_Closed(object sender, RoutedEventArgs e)
        {
            AnimateSortChevron(0);
        }

        private void SortMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            if (item == null) return;
            string mode = item.CommandParameter as string;
            if (string.IsNullOrEmpty(mode)) mode = "recent";
            _store.Settings.SortMode = mode;
            _store.Save(false);
            SortLabel.Text = mode == "title" ? "游戏名称" : (mode == "playtime" ? "游玩时长" : (mode == "added" ? "添加时间" : "最近游玩"));
            AnimateContentReload();
        }
    }
}
