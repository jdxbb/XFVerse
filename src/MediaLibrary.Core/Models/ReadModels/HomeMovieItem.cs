using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class HomeMovieItem : INotifyPropertyChanged
{
    private bool _canContinuePlayback = true;
    private bool _isOpeningPlayback;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int MovieId { get; set; }

    public int? MediaFileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string GenresText { get; set; } = string.Empty;

    public DateTime? Time { get; set; }

    public string LastPlayedText { get; set; } = string.Empty;

    public string LastPlayedAtText { get; set; } = string.Empty;

    public string ProgressText { get; set; } = string.Empty;

    public string ResumePositionText { get; set; } = string.Empty;

    public string ProgressPercentText { get; set; } = string.Empty;

    public string WatchPositionText { get; set; } = string.Empty;

    public double ProgressPercent { get; set; }

    public double ProgressValue { get; set; }

    public bool HasProgress { get; set; }

    public bool HasProgressPosition { get; set; }

    public bool HasProgressPercent { get; set; }

    public bool CanContinuePlayback
    {
        get => _canContinuePlayback;
        set
        {
            if (SetField(ref _canContinuePlayback, value))
            {
                OnPropertyChanged(nameof(CanOpenContinuePlayback));
                OnPropertyChanged(nameof(ContinueButtonText));
            }
        }
    }

    public bool IsOpeningPlayback
    {
        get => _isOpeningPlayback;
        set
        {
            if (SetField(ref _isOpeningPlayback, value))
            {
                OnPropertyChanged(nameof(CanOpenContinuePlayback));
                OnPropertyChanged(nameof(ContinueButtonText));
            }
        }
    }

    public bool CanOpenContinuePlayback => CanContinuePlayback && !IsOpeningPlayback;

    public string ContinueButtonText => !CanContinuePlayback
        ? "播放源不可用"
        : IsOpeningPlayback
            ? "正在打开..."
            : "继续播放";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
