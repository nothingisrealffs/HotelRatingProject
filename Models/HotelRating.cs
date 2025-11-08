namespace HotelRatingViewer.Models
{
    public class HotelRating
    {
        public string HotelName { get; set; } = "";
        public string City { get; set; } = "";
        public string Country { get; set; } = "";
        public double OverallRating { get; set; }
        public double CleanlinessScore { get; set; }
        public double ServiceScore { get; set; }
        public double LocationScore { get; set; }
        public double ComfortScore { get; set; }
        public double PriceScore { get; set; }
        public int TotalReviews { get; set; }
    }
}
