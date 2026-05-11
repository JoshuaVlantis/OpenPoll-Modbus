using Avalonia;
using System;

namespace OpenPoll;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // CLI mode if first arg is a known subcommand; otherwise launch the GUI.
        if (args.Length > 0 && Cli.IsKnownCommand(args[0]))
            return Cli.Run(args);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
