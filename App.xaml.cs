using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace Ludens
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("zh-CN")));

            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            DispatcherUnhandledException += OnDispatcherException;

            AppStore store = AppStore.Instance;
            LauncherService launcher = new LauncherService();
            for (int i = 0; i < store.Games.Count; i++)
            {
                if (store.Games[i].ActiveSessionStartUtc.HasValue)
                {
                    store.RecoverOrphan(store.Games[i]);
                }
            }
            ThemeManager.Apply(store.Settings);

            var window = new MainWindow(store, launcher);
            MainWindow = window;
            window.Show();
        }

        private void OnDispatcherException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception);
            MessageBox.Show("Cloud Game 遇到了一点问题：\n\n" + e.Exception.Message,
                "Cloud Game", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrash(e.ExceptionObject as Exception);
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                AppPaths.Ensure();
                File.AppendAllText(AppPaths.LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\r\n" +
                    (ex == null ? "Unknown error" : ex.ToString()) + "\r\n\r\n");
            }
            catch
            {
            }
        }
    }
}
