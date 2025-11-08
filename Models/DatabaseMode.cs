using System.Collections.Generic;

namespace HotelRatingViewer.Models
{
    public class DatabaseMode
    {
        public bool HasHotelRatingSchema { get; set; }
        public bool HasTables { get; set; }
        public List<string> AvailableSchemas { get; set; } = new();
        public string ErrorMessage { get; set; } = "";
    }
}
