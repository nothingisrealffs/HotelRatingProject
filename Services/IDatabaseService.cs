using System.Collections.Generic;
using System.Threading.Tasks;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public interface IDatabaseService
    {
        string CurrentSchema { get; }
        string ConnectionString { get; }

        Task<bool> ValidateConnectionAsync(string host, string port, string service, string user, string pass);
        Task<bool> ElevateToSystemAsync(string systemPassword);
        Task<bool> RestoreUserSessionAsync();
        
        Task<DatabaseMode> GetDatabaseModeAsync();

        Task<HotelRating?> GetHotelRatingAsync(string hotelName);
        
        // --- NEW METHOD ---
        Task<List<HotelReview>> GetHotelReviewsAsync(string hotelName);
        // ------------------

        Task<List<string>> GetHotelsByCityAsync(string city);
        Task<List<string>> GetCitiesAsync();
        Task<List<string>> GetActiveFeaturesAsync();
        Task<List<HotelListItem>> GetHotelListItemsByCityAsync(string city);
        Task<List<HotelRating>> AdvancedSearchAsync(string city, Dictionary<string, double?> criteria);

        Task<bool> AddFeatureAsync(string featureName);
        Task<bool> AddSeedWordAsync(string featureName, string seedPhrase, int weight);
    }
}