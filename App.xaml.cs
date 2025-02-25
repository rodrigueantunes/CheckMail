using System;
using System.Diagnostics;
using System.Windows;

namespace CheckMail
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Debug.WriteLine("L'application démarre...");

            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
