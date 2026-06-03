using System.Collections.ObjectModel;
using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class SeasonCorrectionSourceMappingRowViewModel : ObservableObject
{
    private readonly Action? _changed;
    private SeasonCorrectionPlaybackSourceOptionViewModel? _selectedSourceOption;
    private string _targetEpisodeNumberText;

    public SeasonCorrectionSourceMappingRowViewModel(
        TvSeasonCorrectionSourceItem source,
        Action? changed = null)
    {
        _changed = changed;
        OriginalEpisodeNumber = source.EpisodeNumber;
        OriginalEpisodeNumberText = source.EpisodeNumberText;
        _targetEpisodeNumberText = source.EpisodeNumber.ToString();
        foreach (var option in source.SourceOptions)
        {
            SourceOptions.Add(new SeasonCorrectionPlaybackSourceOptionViewModel(option));
        }

        if (SourceOptions.Count == 0)
        {
            SourceOptions.Add(SeasonCorrectionPlaybackSourceOptionViewModel.NoSource);
        }

        _selectedSourceOption = SourceOptions.FirstOrDefault(option => option.MediaFileId == source.MediaFileId)
                                ?? SourceOptions.FirstOrDefault();
    }

    public ObservableCollection<SeasonCorrectionPlaybackSourceOptionViewModel> SourceOptions { get; } = [];

    public SeasonCorrectionPlaybackSourceOptionViewModel? SelectedSourceOption
    {
        get => _selectedSourceOption;
        set
        {
            if (SetProperty(ref _selectedSourceOption, value))
            {
                OnPropertyChanged(nameof(MediaFileId));
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(FilePathDisplay));
                OnPropertyChanged(nameof(SourceSummary));
                _changed?.Invoke();
            }
        }
    }

    public int MediaFileId => SelectedSourceOption?.MediaFileId ?? 0;

    public int OriginalEpisodeNumber { get; }

    public string OriginalEpisodeNumberText { get; }

    public string FileName => SelectedSourceOption?.FileName ?? "无播放源";

    public string FilePathDisplay => SelectedSourceOption?.DisplayText ?? "无播放源";

    public string SourceSummary => SelectedSourceOption?.SourceTypeText ?? "无播放源";

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

    public void RestoreSelectedSource(int mediaFileId)
    {
        SelectedSourceOption = SourceOptions.FirstOrDefault(option => option.MediaFileId == mediaFileId)
                               ?? SourceOptions.FirstOrDefault();
    }
}

public sealed class SeasonCorrectionPlaybackSourceOptionViewModel
{
    public static SeasonCorrectionPlaybackSourceOptionViewModel NoSource { get; } = new();

    private SeasonCorrectionPlaybackSourceOptionViewModel()
    {
        MediaFileId = 0;
        FileName = "无播放源";
        DisplayText = "无播放源";
        SourceTypeText = "无播放源";
    }

    public SeasonCorrectionPlaybackSourceOptionViewModel(TvSeasonCorrectionPlaybackSourceItem source)
    {
        MediaFileId = source.MediaFileId;
        FileName = source.SafeFileName;
        DisplayText = source.DisplayText;
        SourceTypeText = source.SourceTypeText;
    }

    public int MediaFileId { get; }

    public string FileName { get; }

    public string DisplayText { get; }

    public string SourceTypeText { get; }
}
