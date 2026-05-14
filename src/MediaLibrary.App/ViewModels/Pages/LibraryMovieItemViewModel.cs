using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.Enums;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class LibraryMovieItemViewModel : ObservableObject
{
    private bool _isBatchSelectionMode;
    private bool _isSelected;

    public LibraryMovieItemViewModel(
        LibraryMovieListItem movie,
        string selectionKey,
        bool isBatchSelectionMode,
        bool isSelected)
    {
        Movie = movie;
        SelectionKey = selectionKey;
        _isBatchSelectionMode = isBatchSelectionMode;
        _isSelected = isSelected;
    }

    public LibraryMovieListItem Movie { get; }

    public string SelectionKey { get; }

    public int MovieId => Movie.MovieId;

    public int SeriesId => Movie.SeriesId;

    public int SeasonId => Movie.SeasonId;

    public bool IsMovie => Movie.IsMovie;

    public bool IsSeries => Movie.IsSeries;

    public bool IsSeason => Movie.IsSeason;

    public string MediaKindText => Movie.MediaKindText;

    public string ProgressSummary => Movie.ProgressSummary;

    public int? TmdbId => Movie.TmdbId;

    public string Title => Movie.Title;

    public string OriginalTitle => Movie.OriginalTitle;

    public int? ReleaseYear => Movie.ReleaseYear;

    public string PosterRemoteUrl => Movie.PosterRemoteUrl;

    public string GenresText => Movie.GenresText;

    public string Overview => Movie.Overview;

    public string Country => Movie.Country;

    public string Language => Movie.Language;

    public int? RuntimeMinutes => Movie.RuntimeMinutes;

    public string ImdbId => Movie.ImdbId;

    public IdentificationStatus IdentificationStatus => Movie.IdentificationStatus;

    public double? PrimaryRatingValue => Movie.PrimaryRatingValue;

    public string PrimaryRatingSourceName => Movie.PrimaryRatingSourceName;

    public int SourceCount => Movie.SourceCount;

    public bool HasLocalSource => Movie.HasLocalSource;

    public bool HasWebDavSource => Movie.HasWebDavSource;

    public string SourceSummary => Movie.SourceSummary;

    public bool IsInLibrary => Movie.IsInLibrary;

    public bool IsFavorite => Movie.IsFavorite;

    public bool IsWatched => Movie.IsWatched;

    public bool IsWantToWatch => Movie.IsWantToWatch;

    public bool IsNotInterested => Movie.IsNotInterested;

    public DateTime UpdatedAt => Movie.UpdatedAt;

    public bool IsBatchSelectionMode
    {
        get => _isBatchSelectionMode;
        set
        {
            if (SetProperty(ref _isBatchSelectionMode, value))
            {
                OnPropertyChanged(nameof(SelectionDotVisible));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool SelectionDotVisible => IsBatchSelectionMode;
}
