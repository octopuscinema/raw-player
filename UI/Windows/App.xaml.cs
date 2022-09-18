using System.Windows;

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
    }
}
