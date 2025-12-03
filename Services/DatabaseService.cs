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
                _originalConnectionString = connStr;
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
                var builder = new OracleConnectionStringBuilder(_connectionString);
                CurrentSchema = builder.UserID.ToUpper();
                return true;
            }
            catch { return false; }
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

                var hasHotel = await CheckTableExistsAsync(conn, "HOTEL");
                var hasReview = await CheckTableExistsAsync(conn, "REVIEW");
                
                if (hasHotel && hasReview) { mode.HasHotelRatingSchema = true; mode.HasTables = true; }
                else { mode.HasHotelRatingSchema = await FindSchemaWithTablesAsync(conn); }

                return mode;
            }
            catch (Exception ex) { mode.ErrorMessage = ex.Message; return mode; }
        }

        private async Task<bool> CheckTableExistsAsync(OracleConnection conn, string tableName)
        {
            try {
                var cmd = new OracleCommand($"SELECT count(*) FROM {tableName} WHERE ROWNUM = 0", conn);
                await cmd.ExecuteScalarAsync();
                return true;
            } catch { return false; }
        }

        private async Task<bool> FindSchemaWithTablesAsync(OracleConnection conn)
        {
            try {
                var cmd = new OracleCommand("SELECT owner FROM all_tables WHERE table_name = 'HOTEL' GROUP BY owner", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) {
                    var schema = reader.GetString(0);
                    try {
                        var checkCmd = new OracleCommand($"SELECT 1 FROM {schema}.Hotel WHERE ROWNUM <= 1", conn);
                        await checkCmd.ExecuteScalarAsync();
                        if (schema != CurrentSchema) { CurrentSchema = schema; }
                        return true;
                    } catch { }
                }
                return false;
            } catch { return false; }
        }

        public async Task<HotelRating?> GetHotelRatingAsync(string hotelName)
        {
            if (string.IsNullOrEmpty(_connectionString)) return null;
            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";
                var ratingTable = string.IsNullOrEmpty(CurrentSchema) ? "Rating" : $"{CurrentSchema}.Rating";
                var featureTable = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";

                // Updated logic: Fetch raw review average as fallback
                var sqlHead = $@"
                    SELECT h.hotel_id, h.hotel_name, h.city, h.country, 
                           (SELECT COUNT(*) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id) as total_reviews,
                           (SELECT AVG(overall_rating) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id) as avg_user_rating
                    FROM {hotelTable} h
                    WHERE UPPER(h.hotel_name) LIKE UPPER(:name)
                    FETCH FIRST 1 ROWS ONLY";
                
                var cmd = new OracleCommand(sqlHead, conn);
                cmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = $"%{hotelName}%";

                HotelRating? rating = null;
                long hotelId = 0;
                double rawFallbackRating = 0;

                using (var reader = await cmd.ExecuteReaderAsync()) {
                    if (await reader.ReadAsync()) {
                        hotelId = reader.GetInt64(reader.GetOrdinal("hotel_id"));
                        var avgOrdinal = reader.GetOrdinal("avg_user_rating");
                        
                        // Safely read the raw average
                        if (!reader.IsDBNull(avgOrdinal))
                        {
                            var val = reader.GetValue(avgOrdinal);
                            rawFallbackRating = Convert.ToDouble(val);
                        }

                        rating = new HotelRating {
                            HotelName = reader.GetString(reader.GetOrdinal("hotel_name")),
                            City = reader.GetString(reader.GetOrdinal("city")),
                            Country = reader.GetString(reader.GetOrdinal("country")),
                            TotalReviews = reader.GetInt32(reader.GetOrdinal("total_reviews"))
                        };
                    }
                }

                if (rating != null && hotelId > 0) {
                    var scoreSql = $@"SELECT f.feature_name, r.score 
                                      FROM {ratingTable} r JOIN {featureTable} f ON r.feature_id = f.feature_id 
                                      WHERE r.hotel_id = :hid";
                    var scoreCmd = new OracleCommand(scoreSql, conn);
                    scoreCmd.Parameters.Add(":hid", OracleDbType.Int64).Value = hotelId;

                    double sumScore = 0; 
                    int count = 0;
                    
                    using (var reader = await scoreCmd.ExecuteReaderAsync()) {
                        while (await reader.ReadAsync()) {
                            var fname = reader.GetString(0).ToUpper();
                            var fscore = reader.GetDouble(1);
                            sumScore += fscore; 
                            count++;

                            if (fname.Contains("CLEAN")) rating.CleanlinessScore = fscore;
                            else if (fname.Contains("SERVICE")) rating.ServiceScore = fscore;
                            else if (fname.Contains("LOCATION")) rating.LocationScore = fscore;
                            else if (fname.Contains("COMFORT")) rating.ComfortScore = fscore;
                            else if (fname.Contains("PRICE") || fname.Contains("VALUE")) rating.PriceScore = fscore;
                        }
                    }

                    // Logic Fix: Use calculated features if available, otherwise fallback to raw user rating
                    if (count > 0) 
                    {
                        rating.OverallRating = Math.Round(sumScore / count, 2);
                    }
                    else 
                    {
                        // Fallback so users don't see 0.00 when reviews exist but features aren't calculated
                        rating.OverallRating = Math.Round(rawFallbackRating, 2);
                    }
                }
                return rating;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error fetching hotel rating"); return null; }
        }

        public async Task<List<HotelReview>> GetHotelReviewsAsync(string hotelName)
        {
            var reviews = new List<HotelReview>();
            if (string.IsNullOrEmpty(_connectionString)) return reviews;

            try
            {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";

                var sql = $@"
                    SELECT r.reviewer_name, r.review_text, r.review_date, r.overall_rating
                    FROM {reviewTable} r
                    JOIN {hotelTable} h ON r.hotel_id = h.hotel_id
                    WHERE UPPER(h.hotel_name) = UPPER(:name)
                    ORDER BY r.review_date DESC
                    FETCH FIRST 50 ROWS ONLY";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = hotelName;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    reviews.Add(new HotelReview
                    {
                        ReviewerName = reader.IsDBNull(0) ? "Anonymous" : reader.GetString(0),
                        ReviewText = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        ReviewDate = reader.IsDBNull(2) ? DateTime.MinValue : reader.GetDateTime(2),
                        Rating = reader.IsDBNull(3) ? 0.0 : Convert.ToDouble(reader.GetValue(3))
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hotel reviews");
            }
            return reviews;
        }

        public async Task<List<string>> GetCitiesAsync()
        {
            var cities = new List<string>();
            if (string.IsNullOrEmpty(_connectionString)) return cities;
            try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var cmd = new OracleCommand($"SELECT DISTINCT city FROM {hotelTable} ORDER BY city", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { if (!reader.IsDBNull(0)) cities.Add(reader.GetString(0)); }
            } catch (Exception ex) { _logger.LogError(ex, "Error loading cities"); }
            return cities;
        }

        public async Task<List<string>> GetActiveFeaturesAsync()
        {
            var features = new List<string>();
            if (string.IsNullOrEmpty(_connectionString)) return features;
            try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                var featureTable = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";
                var cmd = new OracleCommand($"SELECT feature_name FROM {featureTable} WHERE is_active = 'Y' ORDER BY feature_name", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { features.Add(reader.GetString(0)); }
            } catch { }
            return features;
        }

        public async Task<List<string>> GetHotelsByCityAsync(string city)
        {
             return await Task.FromResult(new List<string>());
        }

        public async Task<List<HotelListItem>> GetHotelListItemsByCityAsync(string city)
        {
            var results = new List<HotelListItem>();
            if (string.IsNullOrEmpty(_connectionString)) return results;
            try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                
                // Logic Fix: Do not rely solely on vw_hotel_overall which requires the Rating table to be populated.
                // Use a manual query with COALESCE to fallback to Review.overall_rating.
                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";
                var ratingTable = string.IsNullOrEmpty(CurrentSchema) ? "Rating" : $"{CurrentSchema}.Rating";
                
                var sql = $@"
                    SELECT h.hotel_name, 
                           COALESCE(
                               (SELECT AVG(score) FROM {ratingTable} rt WHERE rt.hotel_id = h.hotel_id),
                               (SELECT AVG(overall_rating) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id),
                               0
                           ) as avg_score,
                           (SELECT COUNT(*) FROM {reviewTable} rv WHERE rv.hotel_id = h.hotel_id) as total_reviews
                    FROM {hotelTable} h
                    WHERE h.city = :city
                    ORDER BY avg_score DESC";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":city", OracleDbType.Varchar2).Value = city;
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) {
                    double rVal = 0.0;
                    if (!reader.IsDBNull(1)) 
                    {
                        // Use GetValue + Convert to handle both DECIMAL and NUMBER types safely
                        rVal = Convert.ToDouble(reader.GetValue(1));
                    }

                    int countVal = 0;
                    if (!reader.IsDBNull(2))
                    {
                        countVal = Convert.ToInt32(reader.GetValue(2));
                    }

                    results.Add(new HotelListItem {
                        Name = reader.GetString(0),
                        Rating = rVal,
                        ReviewCount = countVal
                    });
                }
            } catch (Exception ex) { _logger.LogError(ex, "Error fetching hotel list"); }
            return results;
        }

        public async Task<List<HotelRating>> AdvancedSearchAsync(string city, Dictionary<string, double?> criteria)
        {
            var results = new List<HotelRating>();
            if (string.IsNullOrEmpty(_connectionString)) return results;

            try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();

                var hTbl = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var rTbl = string.IsNullOrEmpty(CurrentSchema) ? "Rating" : $"{CurrentSchema}.Rating";
                var fTbl = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";
                var rvTbl = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";

                var sql = $@"
                    SELECT h.hotel_name, h.city, h.country,
                        (SELECT COUNT(*) FROM {rvTbl} rv WHERE rv.hotel_id = h.hotel_id) as total_reviews,
                        AVG(r.score) as calc_overall,
                        MAX(CASE WHEN UPPER(f.feature_name) LIKE '%CLEAN%' THEN r.score END),
                        MAX(CASE WHEN UPPER(f.feature_name) LIKE '%SERVICE%' THEN r.score END),
                        MAX(CASE WHEN UPPER(f.feature_name) LIKE '%LOCATION%' THEN r.score END),
                        MAX(CASE WHEN UPPER(f.feature_name) LIKE '%COMFORT%' THEN r.score END),
                        MAX(CASE WHEN UPPER(f.feature_name) LIKE '%PRICE%' OR UPPER(f.feature_name) LIKE '%VALUE%' THEN r.score END)
                    FROM {hTbl} h
                    JOIN {rTbl} r ON h.hotel_id = r.hotel_id
                    JOIN {fTbl} f ON r.feature_id = f.feature_id
                    WHERE h.city = :city
                    GROUP BY h.hotel_id, h.hotel_name, h.city, h.country
                    HAVING 1=1 ";

                var parameters = new List<OracleParameter>();
                parameters.Add(new OracleParameter(":city", OracleDbType.Varchar2) { Value = city });

                // Add filters dynamically
                var filterMap = new Dictionary<string, string> {
                    { "overall", "AVG(r.score)" },
                    { "cleanliness", "MAX(CASE WHEN UPPER(f.feature_name) LIKE '%CLEAN%' THEN r.score END)" },
                    { "service", "MAX(CASE WHEN UPPER(f.feature_name) LIKE '%SERVICE%' THEN r.score END)" },
                    { "location", "MAX(CASE WHEN UPPER(f.feature_name) LIKE '%LOCATION%' THEN r.score END)" },
                    { "comfort", "MAX(CASE WHEN UPPER(f.feature_name) LIKE '%COMFORT%' THEN r.score END)" },
                    { "price", "MAX(CASE WHEN UPPER(f.feature_name) LIKE '%PRICE%' OR UPPER(f.feature_name) LIKE '%VALUE%' THEN r.score END)" }
                };

                foreach(var kvp in criteria) {
                    if(kvp.Value.HasValue && filterMap.ContainsKey(kvp.Key)) {
                        sql += $" AND {filterMap[kvp.Key]} >= :p_{kvp.Key}";
                        parameters.Add(new OracleParameter($":p_{kvp.Key}", OracleDbType.Double) { Value = kvp.Value.Value });
                    }
                }

                sql += " ORDER BY calc_overall DESC";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.AddRange(parameters.ToArray());

                using (var reader = await cmd.ExecuteReaderAsync()) {
                    while (await reader.ReadAsync()) {
                        results.Add(new HotelRating {
                            HotelName = reader.GetString(0),
                            City = reader.GetString(1),
                            Country = reader.GetString(2),
                            TotalReviews = reader.GetInt32(3),
                            OverallRating = !reader.IsDBNull(4) ? Math.Round(Convert.ToDouble(reader.GetDecimal(4)), 2) : 0,
                            CleanlinessScore = !reader.IsDBNull(5) ? Math.Round(Convert.ToDouble(reader.GetDecimal(5)), 2) : 0,
                            ServiceScore = !reader.IsDBNull(6) ? Math.Round(Convert.ToDouble(reader.GetDecimal(6)), 2) : 0,
                            LocationScore = !reader.IsDBNull(7) ? Math.Round(Convert.ToDouble(reader.GetDecimal(7)), 2) : 0,
                            ComfortScore = !reader.IsDBNull(8) ? Math.Round(Convert.ToDouble(reader.GetDecimal(8)), 2) : 0,
                            PriceScore = !reader.IsDBNull(9) ? Math.Round(Convert.ToDouble(reader.GetDecimal(9)), 2) : 0
                        });
                    }
                }
            } catch (Exception ex) { _logger.LogError(ex, "Error performing Advanced Search"); }
            return results;
        }

        public async Task<bool> AddFeatureAsync(string featureName)
        {
             if (string.IsNullOrEmpty(_connectionString)) return false;
             try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                var featureTable = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";
                var idCmd = new OracleCommand($"SELECT NVL(MAX(feature_id), 0) + 1 FROM {featureTable}", conn);
                var newId = Convert.ToInt32(await idCmd.ExecuteScalarAsync());
                var cmd = new OracleCommand($"INSERT INTO {featureTable} VALUES (:id, :name, 'User added', 'Y', SYSDATE)", conn);
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = newId;
                cmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = featureName;
                await cmd.ExecuteNonQueryAsync();
                return true;
             } catch { return false; }
        }

        public async Task<bool> AddSeedWordAsync(string featureName, string seedPhrase, int weight)
        {
             if (string.IsNullOrEmpty(_connectionString)) return false;
             try {
                using var conn = new OracleConnection(_connectionString);
                await conn.OpenAsync();
                var featureTable = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";
                var seedTable = string.IsNullOrEmpty(CurrentSchema) ? "Seed" : $"{CurrentSchema}.Seed";
                
                var fidCmd = new OracleCommand($"SELECT feature_id FROM {featureTable} WHERE feature_name = :fname", conn);
                fidCmd.Parameters.Add(":fname", OracleDbType.Varchar2).Value = featureName;
                var fid = await fidCmd.ExecuteScalarAsync();
                if(fid == null) return false;

                var idCmd = new OracleCommand($"SELECT NVL(MAX(seed_id), 0) + 1 FROM {seedTable}", conn);
                var newId = Convert.ToInt32(await idCmd.ExecuteScalarAsync());

                var cmd = new OracleCommand($"INSERT INTO {seedTable} VALUES (:id, :fid, :phrase, :wt, SYSDATE)", conn);
                cmd.Parameters.Add(":id", OracleDbType.Int32).Value = newId;
                cmd.Parameters.Add(":fid", OracleDbType.Int32).Value = Convert.ToInt32(fid);
                cmd.Parameters.Add(":phrase", OracleDbType.Varchar2).Value = seedPhrase;
                cmd.Parameters.Add(":wt", OracleDbType.Int32).Value = weight;
                await cmd.ExecuteNonQueryAsync();
                return true;
             } catch { return false; }
        }
    }
}