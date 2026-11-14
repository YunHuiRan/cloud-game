using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Ludens
{
    /// <summary>
    /// Owns the notification-area icon. The actual menu is a WPF ContextMenu
    /// created by MainWindow so it matches the launcher theme and typography.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private NotifyIcon _icon;

        public event EventHandler ShowRequested;
        public event EventHandler MenuRequested;

        public bool Visible
        {
            get { return _icon != null && _icon.Visible; }
        }

        public TrayIcon()
        {
            _icon = new NotifyIcon();
            _icon.Text = "Cloud Game";
            _icon.Icon = LoadIcon();
            _icon.DoubleClick += delegate { OnShow(); };
            _icon.MouseUp += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right && MenuRequested != null)
                    MenuRequested(this, EventArgs.Empty);
            };
        }

        public void Show()
        {
            if (_icon == null) return;
            _icon.Visible = true;
        }

        public void Hide()
        {
            if (_icon == null) return;
            _icon.Visible = false;
        }

        public void ShowBalloon(string title, string message)
        {
            try
            {
                if (_icon == null || !_icon.Visible) return;
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = message;
                _icon.BalloonTipIcon = ToolTipIcon.Info;
                _icon.ShowBalloonTip(3500);
            }
            catch { }
        }

        private void OnShow()
        {
            if (ShowRequested != null) ShowRequested(this, EventArgs.Empty);
        }

        private static Icon LoadIcon()
        {
            try
            {
                string exe = Assembly.GetEntryAssembly().Location;
                Icon icon = Icon.ExtractAssociatedIcon(exe);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_icon != null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }
        }
    }
}
