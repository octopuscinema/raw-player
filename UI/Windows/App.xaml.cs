using Microsoft.Toolkit.Uwp.Notifications;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Octopus.Player.UI.Windows
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public PlayerApplication PlayerApplication { get; private set; }

        App() : base()
        {
            PlayerApplication = new PlayerApplication();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            PlayerApplication.Dispose();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Open with implementation
            if (e.Args.Length == 1)
                PlayerApplication.OpenOnStart = new string[] { e.Args[0] };

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                ToastArguments arguments = ToastArguments.Parse(toastArgs.Argument);
                var argumentList = new Dictionary<string, string>();
                foreach (var argument in arguments)
                    argumentList[argument.Key] = argument.Value;

                Current.Dispatcher.Invoke(delegate
                {
                    PlayerApplication.OnNotificationClicked((INativeWindow)Current.MainWindow, argumentList);
                });
            };
        }
    }
}
