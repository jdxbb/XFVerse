namespace MediaLibrary.Core.Models.ReadModels;

public sealed class OnlineSubtitleBindingListItem
{
    public int Id { get; set; }

    public int? MovieId { get; set; }

    public int? EpisodeId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string ProviderSubtitleId { get; set; } = string.Empty;

    public string ProviderFileId { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;

    public string LanguageName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ReleaseName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string CacheRelativePath { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public bool HasCacheFile { get; set; }

    public DateTime CreatedAt { get; set; }
}
