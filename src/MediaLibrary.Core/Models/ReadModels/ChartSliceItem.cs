namespace MediaLibrary.Core.Models.ReadModels;

public sealed class ChartSliceItem
{
    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public double Percent { get; set; }
}
