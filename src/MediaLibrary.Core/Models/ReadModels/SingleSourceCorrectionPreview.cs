namespace MediaLibrary.Core.Models.ReadModels;

public sealed class SingleSourceCorrectionPreview
{
    public int MediaFileId { get; set; }

    public SingleSourceCorrectionTargetKind TargetKind { get; set; }

    public bool IsValid { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string CurrentBindingKind { get; set; } = string.Empty;

    public string CurrentBindingTitle { get; set; } = string.Empty;

    public string TargetTypeText { get; set; } = string.Empty;

    public string TargetTitle { get; set; } = string.Empty;

    public bool WillClearMovieId { get; set; }

    public bool WillClearEpisodeId { get; set; }

    public bool WillAppendAsAdditionalSource { get; set; }

    public bool WillCreateTargetContainer { get; set; }

    public bool PreserveProbeFields { get; set; } = true;

    public bool PreserveSubtitleBindings { get; set; } = true;

    public bool DeleteRealFile { get; set; }

    public bool MigrateUserState { get; set; }

    public string PreviewText => string.Join(Environment.NewLine, Lines);

    public IReadOnlyList<string> Lines { get; set; } = [];
}
