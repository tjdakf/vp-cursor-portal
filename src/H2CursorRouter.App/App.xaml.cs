using System.IO;
using System.Reflection;
using System.Windows;
using H2CursorRouter.App.ViewModels;
using H2CursorRouter.Core.Configuration;
using H2CursorRouter.Core.Geometry;
using H2CursorRouter.Core.Validation;
using H2CursorRouter.H2;
using H2CursorRouter.Windows;

namespace H2CursorRouter.App;

public partial class App : Application
{
    private CursorRoutingRuntime? _runtime;
    private IMonitorTopologyService? _monitorTopology;
    private IHotkeyService? _hotkeyService;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var cursorService = new Win32CursorService();
        _monitorTopology = new Win32MonitorTopologyService();
        _monitorTopology.StartWatching(TimeSpan.FromSeconds(2));
        _hotkeyService = new Win32HotkeyService();
        _runtime = new CursorRoutingRuntime(cursorService, _monitorTopology, new CursorRoutingEngine());

        var (configuration, configPath) = await LoadConfigurationAsync();
        var executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
        var fileLogService = new FileLogService(Path.Combine(AppContext.BaseDirectory, "logs"));
        var viewModel = new MainViewModel(
            configuration,
            configPath,
            executablePath,
            new WindowsStartupRegistrationService(),
            fileLogService,
            new H2DeviceClient(),
            cursorService,
            _monitorTopology,
            _runtime,
            new CursorRoutingEngine(),
            new AppConfigurationValidator());

        var startInTray = e.Args.Any(arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase));
        var window = new MainWindow(viewModel, _hotkeyService, startInTray);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _runtime?.Dispose();
        _hotkeyService?.Dispose();
        _monitorTopology?.Dispose();
        base.OnExit(e);
    }

    private static async Task<(AppConfiguration Configuration, string ConfigPath)> LoadConfigurationAsync()
    {
        var configService = new ConfigFileService();
        if (File.Exists("config.json"))
        {
            return (await configService.LoadAsync("config.json"), "config.json");
        }

        if (File.Exists("config.sample.json"))
        {
            return (await configService.LoadAsync("config.sample.json"), "config.json");
        }

        return (SampleConfiguration.Create(), "config.json");
    }
}
