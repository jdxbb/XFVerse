using System.Globalization;
using System.Windows.Data;

namespace MediaLibrary.App.Helpers;

public sealed class RatingStarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double score || score <= 0)
        {
            return "☆☆☆☆☆";
        }

        var stars = Math.Clamp(score / 2d, 0d, 5d);
        var roundedHalfStars = Math.Round(stars * 2d, MidpointRounding.AwayFromZero) / 2d;
        var fullStars = (int)Math.Floor(roundedHalfStars);
        var hasHalfStar = roundedHalfStars - fullStars >= 0.5d && fullStars < 5;
        var emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

        return new string('★', fullStars)
               + (hasHalfStar ? "⯨" : string.Empty)
               + new string('☆', emptyStars);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
