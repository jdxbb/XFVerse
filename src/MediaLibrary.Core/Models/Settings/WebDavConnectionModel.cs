using MediaLibrary.Core.Models.Enums;

namespace MediaLibrary.Core.Models.Settings;

public sealed class WebDavConnectionModel
{
    public int? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ProtocolType ProtocolType { get; set; } = ProtocolType.WebDav;

    public string BaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastConnectedAt { get; set; }

    public DateTime? LastScanAt { get; set; }
}
