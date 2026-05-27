using System.ComponentModel;
using System.Runtime.CompilerServices;
using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.ReadModels;

public sealed class AiRecommendationItem : INotifyPropertyChanged
{
    private bool _isWatched;
    private bool _isWantToWatch;
    private bool _isNotInterested;
    private string _watchStateText = "未看";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int MovieId { get; set; }

    public int? TmdbId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; set; } = string.Empty;

    public int? ReleaseYear { get; set; }

    public DateTime? ReleaseDate { get; set; }

    public string PosterRemoteUrl { get; set; } = string.Empty;

    public string Overview { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Language { get; set; } = string.Empty;

    public int? RuntimeMinutes { get; set; }

    public string ImdbId { get; set; } = string.Empty;

    public double? TmdbRating { get; set; }

    public int? TmdbVoteCount { get; set; }

    public MovieRatingItem? OmdbRating { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string EmotionTagsText { get; set; } = string.Empty;

    public string SceneTagsText { get; set; } = string.Empty;

    public bool IsInLibrary { get; set; }

    public bool IsVisibleInLibrary { get; set; }

    public LibraryVisibilityState LibraryVisibilityState { get; set; } = LibraryVisibilityState.Auto;

    public bool IsWatched
    {
        get => _isWatched;
        set
        {
            if (SetField(ref _isWatched, value))
            {
                OnPropertyChanged(nameof(CanAddWantToWatch));
                OnPropertyChanged(nameof(CanToggleWantToWatch));
                OnPropertyChanged(nameof(WantToWatchButtonText));
                WatchStateText = value ? "已看" : "未看";
            }
        }
    }

    public bool IsWantToWatch
    {
        get => _isWantToWatch;
        set
        {
            if (SetField(ref _isWantToWatch, value))
            {
                OnPropertyChanged(nameof(CanAddWantToWatch));
                OnPropertyChanged(nameof(WantToWatchButtonText));
            }
        }
    }

    public bool IsNotInterested
    {
        get => _isNotInterested;
        set
        {
            if (SetField(ref _isNotInterested, value))
            {
                OnPropertyChanged(nameof(NotInterestedButtonText));
            }
        }
    }

    public string ScopeText { get; set; } = "AI 推荐";

    public string AvailabilityText { get; set; } = "外部候选";

    public string WatchStateText
    {
        get => _watchStateText;
        set => SetField(ref _watchStateText, value);
    }

    public string DetailButtonText => MovieId > 0
        ? IsInLibrary ? "查看详情并播放" : "查看详情（暂无播放源）"
        : "查看详情（外部候选）";

    public bool CanAddWantToWatch => !IsWatched && !IsWantToWatch;

    public bool CanToggleWantToWatch => !IsWatched;

    public string WantToWatchButtonText => IsWantToWatch ? "取消想看" : "+ 想看";

    public string NotInterestedButtonText => IsNotInterested ? "取消不想看" : "不想看";

    public string ReleaseDateText => ReleaseDate.HasValue
        ? ReleaseDate.Value.ToString("yyyy-MM-dd")
        : ReleaseYear?.ToString() ?? "年份 -";

    public string WeightedAverageRatingText
    {
        get
        {
            var ratings = new List<(double Score, int Votes)>();
            if (TmdbRating.HasValue)
            {
                ratings.Add((TmdbRating.Value, Math.Max(TmdbVoteCount ?? 0, 0)));
            }

            if (OmdbRating is { ScoreValue: > 0, ScoreScale: > 0 } omdbRating)
            {
                var normalizedScore = omdbRating.ScoreValue / omdbRating.ScoreScale * 10d;
                ratings.Add((normalizedScore, Math.Max(omdbRating.VoteCount ?? 0, 0)));
            }

            if (ratings.Count == 0)
            {
                return "-";
            }

            var totalVotes = ratings.Sum(x => x.Votes);
            var score = totalVotes > 0
                ? ratings.Sum(x => x.Score * x.Votes) / totalVotes
                : ratings.Average(x => x.Score);

            return $"{score:0.0}";
        }
    }

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
