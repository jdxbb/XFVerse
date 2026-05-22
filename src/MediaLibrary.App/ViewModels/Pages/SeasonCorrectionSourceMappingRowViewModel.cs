using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class SeasonCorrectionSourceMappingRowViewModel : ObservableObject
{
    private readonly Action? _changed;
    private string _targetEpisodeNumberText;

    public SeasonCorrectionSourceMappingRowViewModel(
        TvSeasonCorrectionSourceItem source,
        Action? changed = null)
    {
        _changed = changed;
        MediaFileId = source.MediaFileId;
        OriginalEpisodeNumber = source.EpisodeNumber;
        OriginalEpisodeNumberText = source.EpisodeNumberText;
        FileName = source.SafeFileName;
        SourceSummary = source.SourceSummary;
        _targetEpisodeNumberText = source.EpisodeNumber.ToString();
    }

    public int MediaFileId { get; }

    public int OriginalEpisodeNumber { get; }

    public string OriginalEpisodeNumberText { get; }

    public string FileName { get; }

    public string SourceSummary { get; }

    public string TargetEpisodeNumberText
    {
        get => _targetEpisodeNumberText;
        set
        {
            if (SetProperty(ref _targetEpisodeNumberText, value))
            {
                OnPropertyChanged(nameof(ParsedTargetEpisodeNumber));
                OnPropertyChanged(nameof(IsRemapped));
                OnPropertyChanged(nameof(TargetEpisodeDisplayText));
                _changed?.Invoke();
            }
        }
    }

    public int? ParsedTargetEpisodeNumber => int.TryParse(TargetEpisodeNumberText?.Trim(), out var value) && value > 0
        ? value
        : null;

    public bool IsRemapped => ParsedTargetEpisodeNumber.HasValue && ParsedTargetEpisodeNumber.Value != OriginalEpisodeNumber;

    public string TargetEpisodeDisplayText => ParsedTargetEpisodeNumber is > 0
        ? $"E{ParsedTargetEpisodeNumber.Value:D2}"
        : "-";
}
