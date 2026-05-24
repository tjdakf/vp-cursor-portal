using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using H2CursorRouter.App.Services;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.App.ViewModels;

public sealed class ProfileListViewModel : ViewModelBase
{
    private ProfileRow? _selectedProfile;
    private string _profileFilter = "";
    private readonly IProfileDialogService _profileDialogService;
    private readonly Action<string> _addLog;
    private readonly Action<string> _autoSaveConfiguration;
    private readonly Action _hotkeysChanged;

    public ProfileListViewModel(
        IEnumerable<ProfileRow> profiles,
        IProfileDialogService profileDialogService,
        Action<string> addLog,
        Action<string> autoSaveConfiguration,
        Action hotkeysChanged)
    {
        _profileDialogService = profileDialogService;
        _addLog = addLog;
        _autoSaveConfiguration = autoSaveConfiguration;
        _hotkeysChanged = hotkeysChanged;
        Profiles = new ObservableCollection<ProfileRow>(profiles);
        DashboardProfiles = new ObservableCollection<ProfileRow>();
        FilteredProfiles = CollectionViewSource.GetDefaultView(Profiles);
    }

    public ObservableCollection<ProfileRow> Profiles { get; }
    public ObservableCollection<ProfileRow> DashboardProfiles { get; }
    public ICollectionView FilteredProfiles { get; }

    public void SetFilter(Predicate<object> filter)
    {
        FilteredProfiles.Filter = filter;
    }

    public ProfileRow? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public string ProfileFilter
    {
        get => _profileFilter;
        set
        {
            if (SetProperty(ref _profileFilter, value))
            {
                FilteredProfiles.Refresh();
            }
        }
    }

    public void AddProfile(ProfileEditContext context)
    {
        var profileName = context.GetNextProfileName();
        var result = _profileDialogService.Prompt(
            "Add profile",
            profileName,
            null,
            context.Layouts,
            context.SelectedLayoutId,
            false,
            context.Devices,
            context.Presets,
            null,
            null,
            null,
            null);
        if (result is null)
        {
            return;
        }

        var start = context.CalculateLayoutStart(result.CursorLayoutId);
        var row = new ProfileRow
        {
            Id = context.CreateUniqueProfileId(result.Name),
            Name = result.Name,
            Hotkey = result.Hotkey,
            DeviceId = result.DeviceId,
            ScreenId = result.ScreenId,
            PresetId = result.PresetId,
            PresetDisplayName = result.PresetDisplayName,
            CursorLayoutId = result.CursorLayoutId,
            StartX = start?.X,
            StartY = start?.Y,
            PostAckDelayMs = 500,
            RequireH2AckBeforeCursorLayout = true
        };
        Profiles.Add(row);
        FilteredProfiles.Refresh();
        context.RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        SelectedProfile = row;
        _hotkeysChanged();
        _addLog($"Added profile '{row.Name}'.");
        _autoSaveConfiguration("Auto-saved configuration after adding profile.");
    }

    public void EditProfile(ProfileRow profile, ProfileEditContext context)
    {
        var result = _profileDialogService.Prompt(
            "Edit profile",
            profile.Name,
            profile.Hotkey,
            context.Layouts,
            profile.CursorLayoutId,
            !ProfileHasH2Preset(profile),
            context.Devices,
            context.Presets,
            profile.DeviceId,
            profile.ScreenId,
            profile.PresetId,
            profile.PresetDisplayName);
        if (result is null)
        {
            return;
        }

        var start = context.CalculateLayoutStart(result.CursorLayoutId);
        profile.Name = result.Name;
        profile.Hotkey = result.Hotkey;
        profile.DeviceId = result.DeviceId;
        profile.ScreenId = result.ScreenId;
        profile.PresetId = result.PresetId;
        profile.PresetDisplayName = result.PresetDisplayName;
        profile.CursorLayoutId = result.CursorLayoutId;
        profile.StartX = start?.X;
        profile.StartY = start?.Y;
        profile.PostAckDelayMs = 500;
        profile.RequireH2AckBeforeCursorLayout = true;
        SelectedProfile = profile;
        FilteredProfiles.Refresh();
        context.RefreshProfileLayoutNames();
        RefreshDashboardProfiles();
        _hotkeysChanged();
        _addLog($"Updated profile '{profile.Name}'.");
        _autoSaveConfiguration("Auto-saved configuration after updating profile.");
    }

    public void RemoveSelectedProfile()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        Profiles.Remove(SelectedProfile);
        FilteredProfiles.Refresh();
        RefreshDashboardProfiles();
        SelectedProfile = Profiles.FirstOrDefault();
        _hotkeysChanged();
        _addLog("Removed profile.");
        _autoSaveConfiguration("Auto-saved configuration after removing profile.");
    }

    public void RefreshDashboardProfiles()
    {
        DashboardProfiles.Clear();
        foreach (var profile in Profiles)
        {
            DashboardProfiles.Add(profile);
        }
    }

    private static bool ProfileHasH2Preset(ProfileRow profile) =>
        !string.IsNullOrWhiteSpace(profile.DeviceId) &&
        profile.ScreenId is not null &&
        profile.PresetId is not null;
}

public sealed record ProfileEditContext(
    IReadOnlyList<LayoutRow> Layouts,
    string? SelectedLayoutId,
    IReadOnlyList<DeviceRow> Devices,
    IReadOnlyList<PresetRow> Presets,
    Func<string> GetNextProfileName,
    Func<string, string> CreateUniqueProfileId,
    Func<string?, CursorPoint?> CalculateLayoutStart,
    Action RefreshProfileLayoutNames);
