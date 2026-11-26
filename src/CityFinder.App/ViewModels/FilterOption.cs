using CommunityToolkit.Mvvm.ComponentModel;

namespace CityFinder.App.ViewModels;

public enum PhotoFilter
{
    All,
    ToDo,
    MissingGps,
    Renamed,
}

public sealed class FilterOption : ObservableObject
{
    private int _count;

    public FilterOption(PhotoFilter kind, string name)
    {
        Kind = kind;
        Name = name;
    }

    public PhotoFilter Kind { get; }
    public string Name { get; }

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(Label));
            }
        }
    }

    public string Label => Count > 0 ? $"{Name} ({Count})" : Name;
}
