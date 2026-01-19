using System;
using System.Windows;

namespace AUSUMMARY.Viewer;

/// <summary>
/// Application entry point
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Set up application
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
