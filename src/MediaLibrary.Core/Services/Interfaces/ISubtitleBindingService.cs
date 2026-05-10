namespace MediaLibrary.Core.Services.Interfaces;

public interface ISubtitleBindingService
{
    Task RebuildBindingsAsync(
        int sourceConnectionId,
        IReadOnlyCollection<int> videoMediaFileIds,
        CancellationToken cancellationToken = default);
}
