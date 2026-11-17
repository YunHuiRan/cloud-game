using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ludens.ViewModels;

namespace Ludens
{
    public partial class MainWindow : Window
    {
        private readonly AppStore _store;
        private readonly LauncherService _launcher;
        private readonly Dictionary<UserControl, TranslateTransform> _pageTransforms =
            new Dictionary<UserControl, TranslateTransform>();
        private readonly DispatcherTimer _liveTimer;
        private readonly DispatcherTimer _toastTimer;
        private readonly DispatcherTimer _dropHideTimer;

        private TrayIcon _tray;
        private ContextMenu _trayMenu;
        private UserControl _currentPage;
        private GameEntry _detailGame;
        private bool _detailOpen;
        private GameEntry _pendingDelete;
        private Action _confirmAction;
        private bool _ready;
        private bool _forceClose;

        public MainWindow(AppStore store, LauncherService launcher)
        {
            if (store == null) throw new ArgumentNullException("store");
            if (launcher == null) throw new ArgumentNullException("launcher");
            _store = store;
            _launcher = launcher;
            InitializeComponent();
            DataContext = new ShellViewModel(this);

            _pageTransforms[LibraryPage] = new TranslateTransform();
            _pageTransforms[StatsPage] = new TranslateTransform();
            _pageTransforms[SettingsPage] = new TranslateTransform();
            LibraryPage.RenderTransform = _pageTransforms[LibraryPage];
            StatsPage.RenderTransform = _pageTransforms[StatsPage];
            SettingsPage.RenderTransform = _pageTransforms[SettingsPage];

            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveTimer.Tick += LiveTimer_Tick;
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
            _toastTimer.Tick += delegate { _toastTimer.Stop(); HideToast(); };
            _dropHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
            _dropHideTimer.Tick += delegate { _dropHideTimer.Stop(); HideDropHint(); };

            PreviewDragEnter += Window_PreviewDragEnter;
            PreviewDragOver += Window_PreviewDragOver;
            PreviewDragLeave += Window_PreviewDragLeave;
            PreviewDrop += Window_PreviewDrop;

            Loaded += Window_Loaded;
            Closing += Window_Closing;
            Closed += Window_Closed;
            StateChanged += Window_StateChanged;
            SourceInitialized += delegate { Native.ApplyWindowStyle(this, ThemeManager.IsDark); };

            LibraryPage.AddGameRequested += delegate { OpenEditor(null); };
            LibraryPage.GameOpenRequested += Library_GameOpenRequested;
            LibraryPage.PlayRequested += delegate(object s, GameActionEventArgs e) { LaunchGame(e.Game); };
            LibraryPage.StopRequested += delegate(object s, GameActionEventArgs e) { StopGame(e.Game); };
            LibraryPage.EditRequested += delegate(object s, GameActionEventArgs e) { OpenEditor(e.Game); };
            LibraryPage.DeleteRequested += delegate(object s, GameActionEventArgs e) { AskDelete(e.Game); };

            DetailPage.CloseRequested += delegate { CloseDetail(); };
            DetailPage.PlayRequested += delegate(object s, GameActionEventArgs e) { LaunchGame(e.Game); };
            DetailPage.StopRequested += delegate(object s, GameActionEventArgs e) { StopGame(e.Game); };
            DetailPage.EditRequested += delegate(object s, GameActionEventArgs e) { OpenEditor(e.Game); };
            DetailPage.DeleteRequested += delegate(object s, GameActionEventArgs e) { AskDelete(e.Game); };

            EditorPage.Saved += Editor_Saved;
            EditorPage.Cancelled += delegate { CloseEditor(); };

            ConfirmPage.Confirmed += Confirm_Confirmed;
            ConfirmPage.Cancelled += delegate { CloseConfirm(); };

            SettingsPage.AppearanceChanged += delegate { ApplyAppearanceAnimated(); };
            SettingsPage.BackgroundChanged += delegate { UpdateBackdrop(true); };
            SettingsPage.DimChanged += delegate { UpdateBackdropDim(); };
            SettingsPage.DataChanged += delegate { ReloadEverything(); };
            SettingsPage.Notify += delegate(string message, bool error) { ShowToast(message, error); };

            _store.Changed += Store_Changed;
            _store.GameChanged += Store_GameChanged;
            _store.StoreError += delegate(object s, StoreErrorEventArgs e) { ShowToast(e.Message, true); };

            _launcher.GameStarted += Launcher_GameStarted;
            _launcher.GameEnded += Launcher_GameEnded;

            try
            {
                _tray = new TrayIcon();
                _tray.ShowRequested += delegate { Dispatcher.BeginInvoke(new Action(ShowFromTray)); };
                _tray.MenuRequested += delegate { Dispatcher.BeginInvoke(new Action(ShowTrayMenu)); };
                _tray.Show();
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _ready = true;
            UpdateThemeGlyph();
            UpdateBackdrop();
            RefreshSidebar();
            _currentPage = LibraryPage;
            LibraryPage.Reload(false);
            LibraryPage.Opacity = 1;
            _pageTransforms[LibraryPage].X = 0;
            MoveNavPill(0, false);
            _liveTimer.Start();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (ConfirmPage.IsOpen) { CloseConfirm(); e.Handled = true; return; }
                if (EditorPage.IsOpen) { CloseEditor(); e.Handled = true; return; }
                if (DetailOverlay.Visibility == Visibility.Visible) { CloseDetail(); e.Handled = true; return; }
            }
            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (ConfirmPage.IsOpen || EditorPage.IsOpen) return;
                if (DetailOverlay.Visibility == Visibility.Visible) CloseDetail();
                if (_currentPage != LibraryPage) ShowPage(LibraryPage, 0);
                LibraryPage.FocusSearch();
                e.Handled = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_forceClose) return;
            if (_launcher.RunningCount > 0)
            {
                e.Cancel = true;
                HideToTray("还有游戏正在运行，Cloud Game 已收进托盘。");
                return;
            }
            if (_store.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                HideToTray("Cloud Game 已最小化到托盘。");
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _liveTimer.Stop();
            _toastTimer.Stop();
            try { _launcher.Shutdown(false); }
            catch { }
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
            Application.Current.Shutdown();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (MaximizeGlyph == null) return;
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        private void LiveTimer_Tick(object sender, EventArgs e)
        {
            RefreshSidebar();
            if (_currentPage == LibraryPage) LibraryPage.RefreshLive();
            if (_detailGame != null) DetailPage.UpdateState();
        }

        private void Store_Changed(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { Store_Changed(sender, e); }));
                return;
            }
            if (!_ready) return;
            RefreshSidebar();
            if (_currentPage == LibraryPage) LibraryPage.Reload(true);
            if (_currentPage == StatsPage)
            {
                StatsPage.RunningGames = _launcher.RunningCount;
                StatsPage.Reload();
            }
            if (_detailGame != null)
            {
                if (_store.FindById(_detailGame.Id) == null) CloseDetail();
                else DetailPage.Refresh();
            }
        }

        private void Store_GameChanged(object sender, GameChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { Store_GameChanged(sender, e); }));
                return;
            }
            if (_detailGame != null && e.Game != null && _detailGame.Id == e.Game.Id)
            {
                DetailPage.UpdateState();
                DetailPage.UpdateFavorite();
            }
        }

        private void Launcher_GameStarted(object sender, GameStartedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                LibraryPage.Reload(true);
                RefreshSidebar();
                if (_detailGame != null && _detailGame.Id == e.Game.Id) DetailPage.UpdateState();
                ShowToast("正在游玩 · " + e.Game.Title, false);
            }));
        }

        private void Launcher_GameEnded(object sender, GameEndedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                LibraryPage.Reload(true);
                RefreshSidebar();
                if (_currentPage == StatsPage)
                {
                    StatsPage.RunningGames = _launcher.RunningCount;
                    StatsPage.Reload();
                }
                if (_detailGame != null && _detailGame.Id == e.Game.Id) DetailPage.Refresh();
                ShowToast("本次游玩 " + GameEntry.FormatDuration((long)e.Duration.TotalSeconds), false);
            }));
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            if (sender == NavLibrary) ShowPage(LibraryPage, 0);
            else if (sender == NavStats) ShowPage(StatsPage, 1);
            else if (sender == NavSettings) ShowPage(SettingsPage, 2);
        }

        private void ShowPage(UserControl target, int index)
        {
            if (target == null) return;
            if (target == _currentPage)
            {
                MoveNavPill(index, _store.Settings.Animations);
                if (target == LibraryPage && _detailOpen) ShowDetailOverlay();
                return;
            }

            MoveNavPill(index, _store.Settings.Animations);
            bool reopenDetail = target == LibraryPage && _detailOpen && _detailGame != null;
            if (target == LibraryPage && !reopenDetail) RestoreLibraryAfterDetail();

            if (DetailOverlay.Visibility == Visibility.Visible)
            {
                GhostLayer.Children.Clear();
                DetailPage.SetCoverVisible(true);
                DetailPage.Opacity = 1;
                DetailOverlay.Visibility = Visibility.Collapsed;
                DetailOverlay.Opacity = 1;
            }

            if (target == StatsPage)
            {
                StatsPage.RunningGames = _launcher.RunningCount;
                StatsPage.Reload();
            }
            else if (target == SettingsPage)
            {
                SettingsPage.Reload();
            }
            else if (target == LibraryPage)
            {
                LibraryPage.Reload(true);
            }

            UserControl old = _currentPage;
            _currentPage = target;
            target.Opacity = 0;
            target.Visibility = reopenDetail && target == LibraryPage ? Visibility.Hidden : Visibility.Visible;
            target.IsHitTestVisible = true;

            if (!_store.Settings.Animations)
            {
                if (old != null)
                {
                    old.Visibility = Visibility.Hidden;
                    old.Opacity = 1;
                    _pageTransforms[old].X = 0;
                }
                target.Opacity = 1;
                _pageTransforms[target].X = 0;
                if (reopenDetail) ShowDetailOverlay();
                return;
            }

            if (old != null)
            {
                old.IsHitTestVisible = false;
                var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
                fade.EasingFunction = Motion.EaseOut;
                fade.Completed += delegate
                {
                    if (_currentPage != old)
                    {
                        old.Visibility = Visibility.Hidden;
                        old.Opacity = 1;
                        _pageTransforms[old].X = 0;
                    }
                };
                old.BeginAnimation(OpacityProperty, fade);
                _pageTransforms[old].BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(0, -14, TimeSpan.FromMilliseconds(220)) { EasingFunction = Motion.EaseOut });
            }

            _pageTransforms[target].X = 22;
            Motion.FadeIn(target, 40, 260);
            var slide = new DoubleAnimation(22, 0, TimeSpan.FromMilliseconds(320));
            slide.EasingFunction = Motion.EaseOut;
            _pageTransforms[target].BeginAnimation(TranslateTransform.XProperty, slide);
            if (reopenDetail) ShowDetailOverlay();
        }

        private void HideLibraryForDetail()
        {
            if (LibraryPage == null) return;
            LibraryPage.Visibility = Visibility.Hidden;
            LibraryPage.IsHitTestVisible = false;
        }

        private void RestoreLibraryAfterDetail()
        {
            if (LibraryPage == null) return;
            LibraryPage.Visibility = Visibility.Visible;
            LibraryPage.IsHitTestVisible = true;
            LibraryPage.Opacity = 1;
            if (_pageTransforms.ContainsKey(LibraryPage)) _pageTransforms[LibraryPage].X = 0;
        }

        private void ShowDetailOverlay()
        {
            if (!_detailOpen || _detailGame == null) return;
            HideLibraryForDetail();
            DetailOverlay.Visibility = Visibility.Visible;
            DetailOverlay.Opacity = 1;
            DetailPage.Opacity = 1;
            DetailPage.RenderTransform = new TranslateTransform();
            DetailPage.SetCoverVisible(true);
        }

        private void MoveNavPill(int index, bool animate)
        {
            if (NavPillTransform == null) return;
            double target = index * 54;
            if (!animate)
            {
                NavPillTransform.BeginAnimation(TranslateTransform.YProperty, null);
                NavPillTransform.Y = target;
                return;
            }
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(340));
            animation.EasingFunction = Motion.Emphasized;
            NavPillTransform.BeginAnimation(TranslateTransform.YProperty, animation);
        }

        private void Library_GameOpenRequested(object sender, GameOpenEventArgs e)
        {
            OpenDetail(e.Game, e.SourceRect);
        }

        private void OpenDetail(GameEntry game, Rect sourceRect)
        {
            if (game == null) return;
            _detailGame = game;
            _detailOpen = true;
            DetailPage.Open(game);
            DetailPage.SetCoverVisible(true);
            DetailPage.RenderTransform = new TranslateTransform();
            DetailOverlay.Visibility = Visibility.Visible;
            DetailOverlay.Opacity = 1;
            DetailOverlay.UpdateLayout();

            ImageSource cover = DetailPage.CoverSource;
            Rect target = Motion.ScreenRect(DetailPage.CoverHost);

            if (!_store.Settings.Animations)
            {
                DetailPage.Opacity = 1;
                HideLibraryForDetail();
                return;
            }

            DetailPage.Opacity = 0;
            DetailPage.RenderTransform = new TranslateTransform();
            Motion.FadeIn(DetailPage, 40, 260);

            if (sourceRect.IsEmpty || cover == null || target.IsEmpty || target.Width < 2)
            {
                DetailPage.SetCoverVisible(true);
            }
            else
            {
                DetailPage.SetCoverVisible(false);
                Motion.FlyImage(cover, sourceRect, target, GhostLayer, 17, delegate
                {
                    DetailPage.SetCoverVisible(true);
                });
            }
            HideLibraryForDetail();
        }

        private void CloseDetail()
        {
            if (DetailOverlay.Visibility != Visibility.Visible) return;
            GameEntry closing = _detailGame;
            ImageSource cover = DetailPage.CoverSource;
            Rect from = Motion.ScreenRect(DetailPage.CoverHost);
            _detailOpen = false;
            _detailGame = null;
            RestoreLibraryAfterDetail();
            LibraryPage.UpdateLayout();
            Rect to = closing == null ? Rect.Empty : LibraryPage.GetCoverRect(closing.Id);

            if (!_store.Settings.Animations || cover == null || from.IsEmpty || to.IsEmpty || to.Width < 2)
            {
                DetailOverlay.Visibility = Visibility.Collapsed;
                DetailPage.SetCoverVisible(true);
                DetailPage.Opacity = 1;
                DetailPage.RenderTransform = new TranslateTransform();
                if (closing != null) LibraryPage.SetCoverOpacity(closing.Id, 1, false);
                return;
            }

            if (closing != null) LibraryPage.SetCoverOpacity(closing.Id, 0, false);
            DetailPage.SetCoverVisible(false);
            Border ghost = Motion.FlyImage(cover, from, to, GhostLayer, 13, delegate
            {
                DetailPage.SetCoverVisible(true);
                // The ghost stays parked on the destination while the real cover fades in
                // underneath it, so the cover, time chip and shade arrive together.
                if (closing != null) LibraryPage.SetCoverOpacity(closing.Id, 1, true);
            }, 260);
            if (ghost == null)
            {
                DetailPage.SetCoverVisible(true);
                if (closing != null) LibraryPage.SetCoverOpacity(closing.Id, 1, true);
            }
            var fade = new DoubleAnimation(DetailPage.Opacity, 0, TimeSpan.FromMilliseconds(190));
            fade.EasingFunction = Motion.EaseIn;
            fade.Completed += delegate
            {
                DetailOverlay.Visibility = Visibility.Collapsed;
                DetailOverlay.Opacity = 1;
                DetailPage.Opacity = 1;
                DetailPage.RenderTransform = new TranslateTransform();
            };
            DetailPage.BeginAnimation(OpacityProperty, fade);
            Motion.FadeOut(DetailOverlay, 190, delegate { });
        }

        private void LaunchGame(GameEntry game)
        {
            if (game == null) return;
            try
            {
                if (_launcher.IsRunning(game.Id))
                {
                    StopGame(game);
                    return;
                }
                if (!_launcher.Launch(game))
                    ShowToast("无法启动：" + game.Title, true);
            }
            catch (FileNotFoundException)
            {
                ShowToast("找不到游戏文件，已在编辑器中打开。", true);
                OpenEditor(game);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                ShowToast("启动失败：" + ex.Message, true);
            }
        }

        private void StopGame(GameEntry game)
        {
            if (game == null) return;
            try
            {
                _launcher.Stop(game);
                ShowToast("已停止 · " + game.Title, false);
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                ShowToast("停止失败：" + ex.Message, true);
            }
        }

        private void OpenEditor(GameEntry game)
        {
            if (ConfirmPage.IsOpen) CloseConfirm();
            if (game == null) EditorPage.OpenNew();
            else EditorPage.OpenEdit(game);
            ShowModal(EditorPage.PanelElement, EditorPage.ScrimElement);
        }

        private void OpenEditorFromDrop(string exePath)
        {
            if (EditorPage.IsOpen || ConfirmPage.IsOpen) return;
            EditorPage.OpenNew(exePath);
            ShowModal(EditorPage.PanelElement, EditorPage.ScrimElement);
        }

        private static bool IsDropExtension(string path)
        {
            string ext = Path.GetExtension(path);
            return string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindDropPath(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null) return null;
            for (int i = 0; i < files.Length; i++)
            {
                if (!string.IsNullOrEmpty(files[i]) && File.Exists(files[i]) && IsDropExtension(files[i]))
                    return files[i];
            }
            return null;
        }

        private void Window_PreviewDragEnter(object sender, DragEventArgs e)
        {
            Window_PreviewDragOver(sender, e);
        }

        private void Window_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (FindDropPath(e) != null && !EditorPage.IsOpen && !ConfirmPage.IsOpen)
            {
                e.Effects = DragDropEffects.Copy;
                _dropHideTimer.Stop();
                ShowDropHint();
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_PreviewDragLeave(object sender, DragEventArgs e)
        {
            if (!IsMouseOver) _dropHideTimer.Start();
        }

        private void Window_PreviewDrop(object sender, DragEventArgs e)
        {
            _dropHideTimer.Stop();
            HideDropHint();
            e.Handled = true;
            if (EditorPage.IsOpen || ConfirmPage.IsOpen) return;
            string path = FindDropPath(e);
            if (path == null)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    ShowToast("仅支持 .exe / .bat / .cmd 文件", true);
                return;
            }
            OpenEditorFromDrop(path);
        }

        private void ShowDropHint()
        {
            if (DropHintLayer.Visibility == Visibility.Visible) return;
            DropHintLayer.Visibility = Visibility.Visible;
            DropHintLayer.Opacity = 0;
            DropHintScale.ScaleX = 0.96;
            DropHintScale.ScaleY = 0.96;
            if (_store.Settings.Animations)
            {
                Motion.FadeIn(DropHintLayer, 0, 130);
                var ease = Motion.EaseOut;
                var sx = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
                var sy = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
                DropHintScale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
                DropHintScale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
            }
            else
            {
                DropHintLayer.Opacity = 1;
                DropHintScale.ScaleX = 1;
                DropHintScale.ScaleY = 1;
            }
        }

        private void HideDropHint()
        {
            if (DropHintLayer.Visibility != Visibility.Visible) return;
            if (!_store.Settings.Animations)
            {
                DropHintLayer.Visibility = Visibility.Collapsed;
                return;
            }
            Motion.FadeOut(DropHintLayer, 140, delegate
            {
                DropHintLayer.Visibility = Visibility.Collapsed;
                DropHintScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                DropHintScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            });
        }

        private void Editor_Saved(object sender, GameActionEventArgs e)
        {
            CloseEditor();
            LibraryPage.Reload(true);
            RefreshSidebar();
            if (_detailGame != null) DetailPage.Refresh();
            ShowToast("已保存 · " + (e.Game == null ? "游戏" : e.Game.Title), false);
        }

        private void CloseEditor()
        {
            if (!EditorPage.IsOpen) return;
            CloseModal(EditorPage.PanelElement, EditorPage.ScrimElement, EditorPage.CloseImmediate);
        }

        private void AskDelete(GameEntry game)
        {
            if (game == null) return;
            _pendingDelete = game;
            OpenConfirm("移除游戏",
                "确定要从游戏库移除《" + game.Title + "》吗？",
                "确认移除",
                "游玩记录会一并删除，但不会删除游戏文件。",
                true,
                delegate
                {
                    GameEntry target = _pendingDelete;
                    _pendingDelete = null;
                    if (target == null) return;
                    if (_detailGame != null && _detailGame.Id == target.Id) CloseDetail();
                    _store.DeleteGame(target);
                    RefreshSidebar();
                    ShowToast("已移除 · " + target.Title, false);
                });
        }

        private void OpenConfirm(string title, string message, string confirmLabel, string hint, bool danger, Action action)
        {
            _confirmAction = action;
            ConfirmPage.Open(title, message, confirmLabel, hint, danger);
            ShowModal(ConfirmPage.PanelElement, ConfirmPage.ScrimElement);
        }

        private void Confirm_Confirmed(object sender, EventArgs e)
        {
            Action action = _confirmAction;
            _confirmAction = null;
            CloseConfirm();
            if (action != null) Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
        }

        private void CloseConfirm()
        {
            if (!ConfirmPage.IsOpen) return;
            _confirmAction = null;
            CloseModal(ConfirmPage.PanelElement, ConfirmPage.ScrimElement, ConfirmPage.CloseImmediate);
        }

        private void ShowModal(FrameworkElement panel, FrameworkElement scrim)
        {
            panel.Opacity = 0;
            scrim.Opacity = 0;
            if (!_store.Settings.Animations)
            {
                panel.Opacity = 1;
                scrim.Opacity = 1;
                return;
            }
            Motion.FadeIn(scrim, 0, 180);
            Motion.PopIn(panel, 40, 260);
        }

        private void CloseModal(FrameworkElement panel, FrameworkElement scrim, Action completed)
        {
            if (!_store.Settings.Animations)
            {
                if (completed != null) completed();
                return;
            }
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            fade.EasingFunction = Motion.EaseIn;
            fade.Completed += delegate
            {
                if (completed != null) completed();
            };
            panel.BeginAnimation(OpacityProperty, fade);
            var scrimFade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            scrim.BeginAnimation(OpacityProperty, scrimFade);
        }

        private void RequestExit()
        {
            if (_launcher.RunningCount == 0)
            {
                _forceClose = true;
                _launcher.Shutdown(false);
                Close();
                return;
            }
            if (!IsVisible) ShowFromTray();
            OpenConfirm("退出 Cloud Game",
                "还有 " + _launcher.RunningCount + " 款游戏正在运行。退出会结束游戏进程。",
                "退出并结束游戏",
                "本次游玩时长仍会保存。",
                true,
                delegate
                {
                    _forceClose = true;
                    try { _launcher.Shutdown(true); }
                    catch { }
                    Close();
                });
        }

        private void ShowFromTray()
        {
            if (!IsVisible) Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Native.BringToForeground(this);
        }

        private void ShowTrayMenu()
        {
            if (_trayMenu != null) _trayMenu.IsOpen = false;

            ContextMenu menu = new ContextMenu();
            menu.Width = 232;
            if (_launcher.RunningCount > 0)
            {
                MenuItem status = TrayItem("正在运行 " + _launcher.RunningCount + " 款游戏", "\uE768", null);
                status.IsEnabled = false;
                menu.Items.Add(status);
                menu.Items.Add(new Separator());
            }
            menu.Items.Add(TrayItem("打开 Cloud Game", "\uE8A7", delegate { ShowFromTray(); }));
            menu.Items.Add(TrayItem("添加游戏", "\uE710", delegate
            {
                ShowFromTray();
                OpenEditor(null);
            }));
            menu.Items.Add(TrayItem("设置", "\uE713", delegate
            {
                ShowFromTray();
                ShowPage(SettingsPage, 2);
            }));
            menu.Items.Add(new Separator());
            menu.Items.Add(TrayItem("退出 Cloud Game", "\uE7E8", RequestExit));

            _trayMenu = menu;
            menu.Closed += delegate { if (ReferenceEquals(_trayMenu, menu)) _trayMenu = null; };
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.PlacementTarget = this;
            menu.IsOpen = true;
        }

        private MenuItem TrayItem(string header, string glyph, Action action)
        {
            MenuItem item = new MenuItem();
            item.Header = header;
            item.Tag = glyph;
            if (action != null) item.Click += delegate { action(); };
            return item;
        }

        private void HideToTray(string message)
        {
            Hide();
        }

        private void ApplyAppearanceAnimated()
        {
            if (!_store.Settings.Animations || !IsVisible)
            {
                Root.BeginAnimation(OpacityProperty, null);
                Root.Opacity = 1;
                ApplyAppearance();
                return;
            }
            Root.BeginAnimation(OpacityProperty, null);
            Root.Opacity = 1;
            var fade = new DoubleAnimation(1, 0.58, TimeSpan.FromMilliseconds(90));
            fade.EasingFunction = Motion.EaseIn;
            fade.Completed += delegate
            {
                ApplyAppearance();
                var back = new DoubleAnimation(0.58, 1, TimeSpan.FromMilliseconds(260));
                back.EasingFunction = Motion.EaseOut;
                back.Completed += delegate
                {
                    Root.BeginAnimation(OpacityProperty, null);
                    Root.Opacity = 1;
                };
                Root.BeginAnimation(OpacityProperty, back);
            };
            Root.BeginAnimation(OpacityProperty, fade);
        }

        private void ApplyAppearance()
        {
            ThemeManager.Apply(_store.Settings);
            Native.ApplyWindowStyle(this, ThemeManager.IsDark);
            UpdateBackdrop();
            UpdateThemeGlyph();
            LibraryPage.Reload(false);
            if (_detailGame != null) DetailPage.Refresh();
            if (_currentPage == StatsPage)
            {
                StatsPage.RunningGames = _launcher.RunningCount;
                StatsPage.Reload();
            }
        }

        private void ReloadEverything()
        {
            LibraryPage.Reload(true);
            RefreshSidebar();
            if (_currentPage == StatsPage)
            {
                StatsPage.RunningGames = _launcher.RunningCount;
                StatsPage.Reload();
            }
            if (_detailGame != null)
            {
                if (_store.FindById(_detailGame.Id) == null) CloseDetail();
                else DetailPage.Refresh();
            }
            SettingsPage.Reload();
        }

        private void UpdateBackdrop()
        {
            UpdateBackdrop(false);
        }

        private void UpdateBackdropDim()
        {
            AppSettings settings = _store.Settings;
            bool hasImage = !string.IsNullOrEmpty(settings.BackgroundImage);
            BackdropScrim.Opacity = hasImage
                ? Math.Min(0.72, 0.14 + settings.BackgroundDim * 0.58)
                : 0;
        }

        private void UpdateBackdrop(bool animate)
        {
            AppSettings settings = _store.Settings;
            ImageSource image = null;
            if (!string.IsNullOrEmpty(settings.BackgroundImage))
                image = MediaService.LoadAppBackground();

            double targetOpacity = image == null ? 0 : (ThemeManager.IsDark ? 0.62 : 0.42);
            Action apply = delegate
            {
                AppBackdrop.BeginAnimation(OpacityProperty, null);
                AppBackdrop.Source = image;
                AppBackdrop.Opacity = 0;
                BackdropWash.Opacity = image == null ? 0 : (ThemeManager.IsDark ? 0.24 : 0.52);
                BackdropScrim.Opacity = image == null ? 0 : Math.Min(0.72, 0.14 + settings.BackgroundDim * 0.58);
                if (image != null)
                {
                    var fadeIn = new DoubleAnimation(0, targetOpacity, TimeSpan.FromMilliseconds(280));
                    fadeIn.EasingFunction = Motion.EaseOut;
                    AppBackdrop.BeginAnimation(OpacityProperty, fadeIn);
                }
            };

            if (animate && AppBackdrop.Source != null && IsVisible)
            {
                var fadeOut = new DoubleAnimation(AppBackdrop.Opacity, 0, TimeSpan.FromMilliseconds(120));
                fadeOut.EasingFunction = Motion.EaseIn;
                fadeOut.Completed += delegate { apply(); };
                AppBackdrop.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                apply();
            }

            Color accent = ThemeManager.AccentColor;
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(ThemeManager.WithAlpha(accent, 0.24), 0));
            glow.GradientStops.Add(new GradientStop(ThemeManager.WithAlpha(accent, 0.08), 0.48));
            glow.GradientStops.Add(new GradientStop(ThemeManager.WithAlpha(accent, 0), 1));
            AccentGlow.Fill = glow;
            AccentGlow.Opacity = ThemeManager.IsDark ? 1 : 0.65;
        }

        private void UpdateThemeGlyph()
        {
            if (QuickThemeGlyph == null) return;
            QuickThemeGlyph.Text = ThemeManager.IsDark ? "\uE706" : "\uE708";
            QuickThemeButton.ToolTip = ThemeManager.IsDark ? "切换到亮色模式" : "切换到暗色模式";
        }

        private void QuickTheme_Click(object sender, RoutedEventArgs e)
        {
            _store.Settings.Theme = ThemeManager.IsDark ? "light" : "dark";
            _store.Save();
            ApplyAppearanceAnimated();
            SettingsPage.Reload();
        }

        private void RefreshSidebar()
        {
            if (SidebarWeekText == null) return;
            long week = _store.WeekPlaySeconds;
            SidebarWeekText.Text = Compact(week);
            SidebarGamesText.Text = _store.Games.Count.ToString() + " 款游戏";

            List<GameEntry> running = _launcher.GetRunningGames();
            if (running.Count == 0)
            {
                NowPlayingPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                NowPlayingPanel.Visibility = Visibility.Visible;
                GameEntry first = running[0];
                NowPlayingTitle.Text = running.Count > 1 ? first.Title + "  +" + (running.Count - 1) : first.Title;
                NowPlayingTime.Text = Compact((long)_launcher.GetRunningDuration(first.Id).TotalSeconds);
            }
        }

        private static string Compact(long seconds)
        {
            if (seconds < 60) return "0 m";
            if (seconds < 3600) return (seconds / 60) + " m";
            double hours = seconds / 3600.0;
            if (hours < 100) return hours.ToString("0.#") + " h";
            return ((int)hours) + " h";
        }

        private void ShowToast(string message, bool error)
        {
            if (string.IsNullOrEmpty(message)) return;
            ToastText.Text = message;
            ToastAccentLine.Background = ThemeManager.Brush(error ? "Danger" : "Success");
            ToastGlyph.Foreground = ThemeManager.Brush(error ? "Danger" : "Success");
            ToastGlyph.Text = error ? "\uEA39" : "\uE73E";
            ToastHost.Visibility = Visibility.Visible;
            ToastBorder.Opacity = 0;
            ToastTransform.Y = 12;

            if (_store.Settings.Animations)
            {
                Motion.FadeIn(ToastBorder, 0, 180);
                var slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(260));
                slide.EasingFunction = Motion.EaseOut;
                ToastTransform.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            else
            {
                ToastBorder.Opacity = 1;
                ToastTransform.Y = 0;
            }

            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            if (!_store.Settings.Animations)
            {
                ToastHost.Visibility = Visibility.Collapsed;
                return;
            }
            Motion.FadeOut(ToastBorder, 160, delegate { ToastHost.Visibility = Visibility.Collapsed; });
        }
    }
}
