using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ludens.Views
{
    public partial class ConfirmDialog : UserControl
    {
        private bool _danger;

        public event EventHandler Confirmed;
        public event EventHandler Cancelled;

        public bool IsOpen { get; private set; }
        public FrameworkElement PanelElement { get { return DialogPanel; } }
        public FrameworkElement ScrimElement { get { return Scrim; } }

        public ConfirmDialog()
        {
            InitializeComponent();
        }

        public void Open(string title, string message, string confirmLabel, string hint, bool danger)
        {
            _danger = danger;
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            DialogHint.Text = string.IsNullOrEmpty(hint) ? "这个操作无法撤销。" : hint;
            ConfirmButton.Content = confirmLabel;
            ConfirmButton.Style = (Style)Application.Current.FindResource(danger ? "DangerButton" : "PrimaryButton");
            ConfirmButton.IsDefault = true;
            ConfirmButton.IsCancel = false;
            IsOpen = true;
            Visibility = Visibility.Visible;
            ConfirmButton.Focus();
        }

        public void CloseImmediate()
        {
            IsOpen = false;
            Visibility = Visibility.Collapsed;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (Confirmed != null) Confirmed(this, EventArgs.Empty);
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
