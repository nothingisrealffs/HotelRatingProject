using System.Collections.Generic;
using System.Threading.Tasks;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public interface IDatabaseService
    {
        // Properties
        string CurrentSchema { get; }
        string ConnectionString { get; }

        // Connection & Auth
        Task<bool> ValidateConnectionAsync(string host, string port, string service, string user, string pass);
        Task<bool> ElevateToSystemAsync(string systemPassword);
        Task<bool> RestoreUserSessionAsync();
        
        /// <summary>
        /// Checks the connected database for necessary tables and views.
        /// Returns a mode indicating capabilities (e.g. has hotel schema, etc).
        /// </summary>
        Task<DatabaseMode> GetDatabaseModeAsync();

        // read/query operations used by the UI
        Task<HotelRating?> GetHotelRatingAsync(string hotelName);
        Task<List<string>> GetHotelsByCityAsync(string city);
        Task<List<string>> GetCitiesAsync();
        Task<List<string>> GetActiveFeaturesAsync();
        Task<List<HotelListItem>> GetHotelListItemsByCityAsync(string city);
        Task<List<HotelRating>> AdvancedSearchAsync(string city, Dictionary<string, double?> criteria);

        // admin write operations
        Task<bool> AddFeatureAsync(string featureName);
        Task<bool> AddSeedWordAsync(string featureName, string seedPhrase, int weight);

    }
}
