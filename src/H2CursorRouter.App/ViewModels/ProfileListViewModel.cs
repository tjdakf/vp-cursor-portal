using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;

namespace H2CursorRouter.App.ViewModels;

public sealed class ProfileListViewModel : ViewModelBase
{
    private ProfileRow? _selectedProfile;
    private string _profileFilter = "";

    public ProfileListViewModel(IEnumerable<ProfileRow> profiles)
    {
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
}
