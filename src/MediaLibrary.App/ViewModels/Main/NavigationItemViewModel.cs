using MediaLibrary.App.Models.Enums;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.App.ViewModels.Pages;

namespace MediaLibrary.App.ViewModels.Main;

public sealed class NavigationItemViewModel : ViewModelBase
{
    public NavigationItemViewModel(NavigationPageKey pageKey, string title, PageViewModelBase pageViewModel)
    {
        PageKey = pageKey;
        Title = title;
        PageViewModel = pageViewModel;
    }

    public NavigationPageKey PageKey { get; }

    public string Title { get; }

    public PageViewModelBase PageViewModel { get; }
}
