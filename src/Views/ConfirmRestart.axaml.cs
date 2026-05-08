using System.Diagnostics;

using Avalonia.Interactivity;

namespace GetHub.Views
{
    public partial class ConfirmRestart : ChromelessWindow
    {
        public ConfirmRestart()
        {
            InitializeComponent();
        }

        private void Restart(object _1, RoutedEventArgs _2)
        {
            var selfExecFile = Process.GetCurrentProcess().MainModule!.FileName;
            Process.Start(selfExecFile);
            App.Quit(-1);
        }
    }
}
