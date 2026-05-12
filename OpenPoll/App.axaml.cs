using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenPoll.Services;
using OpenPoll.Views;

namespace OpenPoll;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        FileLogger.Start();
        if (FileLogger.CurrentPath is not null)
            System.Console.WriteLine($"OpenPoll logging to {FileLogger.CurrentPath}");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new HomeView();
            desktop.Exit += (_, _) => FileLogger.Stop();
        }

        base.OnFrameworkInitializationCompleted();
    }
}