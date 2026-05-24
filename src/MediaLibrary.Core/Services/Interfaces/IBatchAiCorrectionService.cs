using MediaLibrary.Core.Models.ReadModels;

namespace MediaLibrary.Core.Services.Interfaces;

public interface IBatchAiCorrectionService
{
    Task<BatchAiCorrectionRunResult> CorrectAsync(
        IReadOnlyCollection<BatchAiCorrectionSelectionItem> selections,
        IProgress<BatchAiCorrectionProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
