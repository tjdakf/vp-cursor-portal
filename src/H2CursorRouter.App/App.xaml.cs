using System.IO;
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

        var configuration = await LoadConfigurationAsync();
        var viewModel = new MainViewModel(
            configuration,
            new H2DeviceClient(),
            cursorService,
            _monitorTopology,
            _runtime,
            new CursorRoutingEngine(),
            new AppConfigurationValidator());

        var window = new MainWindow(viewModel, _hotkeyService);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _runtime?.Dispose();
        _hotkeyService?.Dispose();
        _monitorTopology?.Dispose();
        base.OnExit(e);
    }

    private static async Task<AppConfiguration> LoadConfigurationAsync()
    {
        var configService = new ConfigFileService();
        foreach (var path in new[] { "config.json", "config.sample.json" })
        {
            if (File.Exists(path))
            {
                return await configService.LoadAsync(path);
            }
        }

        return SampleConfiguration.Create();
    }
}
