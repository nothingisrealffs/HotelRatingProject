using Avalonia.Media;

namespace HotelRatingViewer.Helpers
{
    public static class RatingColorHelper
    {
        public static IImmutableSolidColorBrush GetRatingColor(double score)
        {
            if (score >= 4.0) return Brushes.Green;
            if (score >= 3.0) return Brushes.Orange;
            return Brushes.Red;
        }
    }
}
