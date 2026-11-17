using System;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Ludens.Infrastructure;

namespace Ludens.ViewModels
{
    /// <summary>
    /// State and commands owned by the application shell. Page-specific interaction
    /// remains in existing views during incremental migration to retain layout and animation behavior.
    /// </summary>
    public sealed class ShellViewModel : ObservableObject
    {
        private readonly Window _window;
        private readonly string _versionText;

        public ShellViewModel(Window window)
        {
            _window = window;
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            _versionText = "v" + (version == null ? "2.1.0" : version.ToString(3));
            MinimizeCommand = new RelayCommand(delegate { _window.WindowState = WindowState.Minimized; });
            ToggleMaximizeCommand = new RelayCommand(ToggleMaximize);
            CloseCommand = new RelayCommand(delegate { _window.Close(); });
        }

        public string VersionText
        {
            get { return _versionText; }
        }

        public ICommand MinimizeCommand { get; private set; }
        public ICommand ToggleMaximizeCommand { get; private set; }
        public ICommand CloseCommand { get; private set; }

        private void ToggleMaximize()
        {
            _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
    }
}