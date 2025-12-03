// File: ./Models/HotelListItem.cs

namespace HotelRatingViewer.Models
{
    public class HotelListItem
    {
        public string Name { get; set; } = "";
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
    }
}