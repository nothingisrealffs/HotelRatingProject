using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly ILogger<DatabaseService> _logger;
        private string _connectionString = "";
        private string _originalConnectionString = "";
        
        public string CurrentSchema { get; private set; } = "";
        public string ConnectionString => _connectionString;

        public DatabaseService(ILogger<DatabaseService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ValidateConnectionAsync(string host, string port, string service, string user, string pass)
        {
            try
            {
                var builder = new OracleConnectionStringBuilder
                {
                    DataSource = $"(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={service})))",
                    UserID = user,
                    Password = pass
                };

                var connStr = builder.ConnectionString;
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                
                _connectionString = connStr;
                _originalConnectionString = connStr; // Save for restore
                
                // Set default schema to user
                CurrentSchema = user.ToUpper();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection validation failed");
                return false;
            }
        }

        public async Task<bool> ElevateToSystemAsync(string systemPassword)
        {
            // Rebuild connection string for SYSTEM
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString)
                {
                    UserID = "SYSTEM",
                    Password = systemPassword
                };
                
                using var conn = new OracleConnection(builder.ConnectionString);
                await conn.OpenAsync();
                
                _connectionString = builder.ConnectionString;
                CurrentSchema = "SYSTEM";
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to elevate to SYSTEM");
                return false;
            }
        }

        public async Task<bool> RestoreUserSessionAsync()
        {
            if (string.IsNullOrEmpty(_originalConnectionString)) return false;
            
            try
            {
                using var conn = new OracleConnection(_originalConnectionString);
                await conn.OpenAsync();
                
                _connectionString = _originalConnectionString;
                // Extract user from original string or saved state
                var builder = new OracleConnectionStringBuilder(_connectionString);
                CurrentSchema = builder.UserID.ToUpper();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<DatabaseMode> GetDatabaseModeAsync()
        {
            var mode = new DatabaseMode();
            if (string.IsNullOrEmpty(_connectionString))
            {
                mode.ErrorMessage = "Not connected";
                return mode;
            }

            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                // Check for Tables (Hotel, Review, etc)
                var hasHotel = await CheckTableExistsAsync(conn, "HOTEL");
                var hasReview = await CheckTableExistsAsync(conn, "REVIEW");
                
                if (hasHotel && hasReview)
                {
                    mode.HasHotelRatingSchema = true;
                    mode.HasTables = true;
                }
                else
                {
                    // Try to find them in other schemas if we are SYSTEM or have access
                     mode.HasHotelRatingSchema = await FindSchemaWithTablesAsync(conn);
                }

                return mode;
            }
            catch (Exception ex)
            {
                mode.ErrorMessage = ex.Message;
                return mode;
            }
        }

        private async Task<bool> CheckTableExistsAsync(OracleConnection conn, string tableName)
        {
            try
            {
                // Simple check
                var cmd = new OracleCommand($"SELECT count(*) FROM {tableName} WHERE ROWNUM = 0", conn);
                await cmd.ExecuteScalarAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> FindSchemaWithTablesAsync(OracleConnection conn)
        {
            // Check common schemas or ALL_TABLES
            try
            {
                var cmd = new OracleCommand("SELECT owner FROM all_tables WHERE table_name = 'HOTEL' GROUP BY owner", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var schema = reader.GetString(0);
                    // Verify Review also exists
                    try
                    {
                        var checkCmd = new OracleCommand($"SELECT 1 FROM {schema}.Hotel WHERE ROWNUM <= 1", conn);
                        await checkCmd.ExecuteScalarAsync();
                        
                        if (schema != CurrentSchema)
                        {
                            CurrentSchema = schema;
                            _logger.LogInformation("Hotel schema found in alternate schema: {Schema}", schema);
                        }
                        return true;
                    }
                    catch { }
                }
                _logger.LogWarning("Hotel schema not found in any accessible schema");
                return false;
            }
            catch
            {
                return false;
            }
        }

        // -----------------------
        // Query implementations
        // -----------------------

        public async Task<HotelRating?> GetHotelRatingAsync(string hotelName)
        {
            if (string.IsNullOrEmpty(_connectionString)) return null;
            
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";

                var sql = $@"
                    SELECT h.hotel_id, h.hotel_name, h.city, h.country, 
                           (SELECT COUNT(*) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id) as total_reviews
                    FROM {hotelTable} h
                    WHERE UPPER(h.hotel_name) LIKE UPPER(:name)
                    FETCH FIRST 1 ROWS ONLY";
                
                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = $"%{hotelName}%";
                
                HotelRating? rating = null;

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        rating = new HotelRating
                        {
                            HotelName = reader.GetString(reader.GetOrdinal("hotel_name")),
                            City = reader.GetString(reader.GetOrdinal("city")),
                            Country = reader.GetString(reader.GetOrdinal("country")),
                            TotalReviews = reader.GetInt32(reader.GetOrdinal("total_reviews"))
                        };
                    }
                }
                
                return rating;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hotel rating");
                return null;
            }
        }

        public async Task<List<string>> GetCitiesAsync()
        {
            var cities = new List<string>();
            if (string.IsNullOrEmpty(_connectionString)) return cities;

            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var cmd = new OracleCommand($"SELECT DISTINCT city FROM {hotelTable} ORDER BY city", conn);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    cities.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cities");
            }
            return cities;
        }

        public async Task<List<string>> GetActiveFeaturesAsync()
        {
             // Placeholder implementation to match interface
            await Task.CompletedTask;
            return new List<string>();
        }

        public async Task<List<string>> GetHotelsByCityAsync(string city)
        {
            var hotels = new List<string>();
            if (string.IsNullOrEmpty(_connectionString)) return hotels;

            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var cmd = new OracleCommand($"SELECT hotel_name FROM {hotelTable} WHERE city = :city ORDER BY hotel_name", conn);
                cmd.Parameters.Add(":city", OracleDbType.Varchar2).Value = city;
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    hotels.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hotels for city: {City}", city);
            }
            return hotels;
        }

        public async Task<List<HotelListItem>> GetHotelListItemsByCityAsync(string city)
        {
            var results = new List<HotelListItem>();
            if (string.IsNullOrEmpty(_connectionString)) return results;

            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                var viewRef = string.IsNullOrEmpty(CurrentSchema) ? "vw_hotel_ratings" : $"{CurrentSchema}.vw_hotel_ratings";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";

                var sql = $@"
                    SELECT v.hotel_name, AVG(v.score) as overall_rating, 
                           (SELECT COUNT(*) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id) as review_count
                    FROM {viewRef} v
                    JOIN {hotelTable} h ON v.hotel_name = h.hotel_name
                    WHERE v.city = :city
                    GROUP BY v.hotel_name, h.hotel_id
                    ORDER BY v.hotel_name";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":city", OracleDbType.Varchar2).Value = city;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new HotelListItem
                    {
                        Name = reader.GetString(0),
                        Rating = Math.Round(reader.GetDouble(1), 2),
                        ReviewCount = reader.GetInt32(2)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hotel list items for city: {City}", city);
            }
            return results;
        }

        public async Task<List<HotelRating>> AdvancedSearchAsync(string city, Dictionary<string, double?> criteria)
        {
            var results = new List<HotelRating>();
            if (string.IsNullOrEmpty(_connectionString)) return results;
            await Task.CompletedTask;
            // Placeholder
            return results;
        }

        public async Task<bool> AddFeatureAsync(string featureName)
        {
             if (string.IsNullOrEmpty(_connectionString)) return false;
             await Task.CompletedTask;
            // Placeholder; replace with original implementation if available.
            return false;
        }

        public async Task<bool> AddSeedWordAsync(string featureName, string seedPhrase, int weight)
        {
             if (string.IsNullOrEmpty(_connectionString)) return false;
             await Task.CompletedTask;
            // Placeholder; replace with original implementation if available.
            return false;
        }
    }
}
