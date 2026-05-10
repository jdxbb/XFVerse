namespace MediaLibrary.App.Models.Player;

public sealed class PlayerPreferencesModel
{
    public int Volume { get; set; } = 80;

    public bool Muted { get; set; }

    public int Brightness { get; set; } = 100;
}
