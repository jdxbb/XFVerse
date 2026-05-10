using MediaLibrary.App.ViewModels.Base;

namespace MediaLibrary.App.ViewModels.Pages;

public abstract class PageViewModelBase : ViewModelBase
{
    protected PageViewModelBase(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public virtual bool IsRefreshing => false;

    public virtual Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public virtual void Deactivate()
    {
    }
}
