using MediaLibrary.App.ViewModels.Base;
using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.App.ViewModels.Pages;

public sealed class ManualUnknownSeasonAggregationSourceRowViewModel : ObservableObject
{
    private string _episodeNumberText;

    public ManualUnknownSeasonAggregationSourceRowViewModel(ManualUnknownSeasonAggregationSourceItem source)
    {
        MediaFileId = source.MediaFileId;
        SortIndex = source.SortIndex;
        FileName = source.FileName;
        SourceSummary = source.SourceSummary;
        CurrentBindingText = source.CurrentBindingText;
        EpisodeNumberParsedFromFileName = source.EpisodeNumberParsedFromFileName;
        _episodeNumberText = source.SuggestedEpisodeNumber.ToString();
    }

    public int MediaFileId { get; }

    public int SortIndex { get; }

    public string FileName { get; }

    public string SourceSummary { get; }

    public string CurrentBindingText { get; }

    public bool EpisodeNumberParsedFromFileName { get; }

    public string EpisodeNumberSourceText => EpisodeNumberParsedFromFileName ? "文件名解析" : "按排序填充";

    public string EpisodeNumberText
    {
        get => _episodeNumberText;
        set => SetProperty(ref _episodeNumberText, value);
    }

    public int? ParsedEpisodeNumber => int.TryParse(EpisodeNumberText, out var value) && value > 0
        ? value
        : null;
}
