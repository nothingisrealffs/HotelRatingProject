using System;

namespace HotelRatingViewer.Models
{
    public class HotelReview
    {
        public string ReviewerName { get; set; } = "Anonymous";
        public string ReviewText { get; set; } = "";
        public DateTime ReviewDate { get; set; }
        public double Rating { get; set; }
    }
}