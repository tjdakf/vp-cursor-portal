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

public partial class App : System.Windows.Application
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
            new DisplayIdentificationService(),
            new TextInputDialogService(),
            new ConfirmationDialogService(),
            new ProfileDialogService(),
            new DeviceDialogService(),
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
        var baseDirectory = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDirectory, "config.json");
        var sampleConfigPath = Path.Combine(baseDirectory, "config.sample.json");

        if (File.Exists(configPath))
        {
            return (await configService.LoadAsync(configPath), configPath);
        }

        if (File.Exists(sampleConfigPath))
        {
            return (await configService.LoadAsync(sampleConfigPath), configPath);
        }

        return (SampleConfiguration.Create(), configPath);
    }
}
