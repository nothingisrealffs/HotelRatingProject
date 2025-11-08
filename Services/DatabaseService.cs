using System;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;
using HotelRatingViewer.Models;

namespace HotelRatingViewer.Services
{
    public class DatabaseService : IAuthenticationService
    {
        public string CurrentSchema { get; private set; } = "";
        private string _connectionString = "";

        public bool ValidateConnection(string server, string port, string service, 
                                       string username, string password,
                                       out string connectionString, 
                                       out DatabaseMode dbMode)
        {
            connectionString = "";
            dbMode = new DatabaseMode();

            try
            {
                connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={server})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={service})));User Id={username};Password={password};";
                _connectionString = connectionString;

                using var conn = new OracleConnection(connectionString);
                conn.Open();

                CurrentSchema = conn.GetType().GetProperty("DatabaseName")?.GetValue(conn)?.ToString() ?? "";

                DetectDatabaseMode(conn, dbMode);

                return true;
            }
            catch (Exception ex)
            {
                dbMode.ErrorMessage = ex.Message;
                return false;
            }
        }

        private void DetectDatabaseMode(OracleConnection conn, DatabaseMode dbMode)
        {
            // Get accessible schemas/users
            var cmd = new OracleCommand("SELECT DISTINCT OWNER FROM ALL_TABLES ORDER BY OWNER", conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                dbMode.AvailableSchemas.Add(reader.GetString(0));
            }
            reader.Close();

            dbMode.HasTables = dbMode.AvailableSchemas.Count > 0;

            // Check for hotel rating schema in current user's schema first
            try
            {
                var checkCmd = new OracleCommand("SELECT 1 FROM Hotel WHERE ROWNUM <= 1", conn);
                checkCmd.ExecuteScalar();
                dbMode.HasHotelRatingSchema = true;
                CurrentSchema = conn.GetType().GetProperty("UserId")?.GetValue(conn)?.ToString() ?? "";
            }
            catch
            {
                // Check other accessible schemas
                foreach (var schema in dbMode.AvailableSchemas)
                {
                    try
                    {
                        var checkCmd = new OracleCommand($"SELECT 1 FROM {schema}.Hotel WHERE ROWNUM <= 1", conn);
                        checkCmd.ExecuteScalar();
                        dbMode.HasHotelRatingSchema = true;
                        CurrentSchema = schema;
                        break;
                    }
                    catch
                    {
                        // Table doesn't exist in this schema, continue
                    }
                }
            }
        }

        public HotelRating? GetHotelRating(string hotelName)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var reviewTable = string.IsNullOrEmpty(CurrentSchema) ? "Review" : $"{CurrentSchema}.Review";

                var sql = $@"
                    SELECT 
                        h.hotel_name,
                        h.city,
                        h.country,
                        COUNT(DISTINCT rv.review_id) as total_reviews
                    FROM {hotelTable} h
                    LEFT JOIN {reviewTable} rv ON h.hotel_id = rv.hotel_id
                    WHERE UPPER(h.hotel_name) LIKE UPPER(:name)
                    GROUP BY h.hotel_id, h.hotel_name, h.city, h.country
                    FETCH FIRST 1 ROWS ONLY";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = $"%{hotelName}%";

                HotelRating? rating = null;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
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

                if (rating != null)
                {
                    LoadFeatureScores(conn, rating);
                }

                return rating;
            }
            catch
            {
                return null;
            }
        }

        private void LoadFeatureScores(OracleConnection conn, HotelRating rating)
        {
            var viewRef = string.IsNullOrEmpty(CurrentSchema) ? "vw_hotel_ratings" : $"{CurrentSchema}.vw_hotel_ratings";
            var scoresSql = $@"
                SELECT 
                    feature_name,
                    score
                FROM {viewRef}
                WHERE hotel_name = :name";

            var scoresCmd = new OracleCommand(scoresSql, conn);
            scoresCmd.Parameters.Add(":name", OracleDbType.Varchar2).Value = rating.HotelName;

            using var scoresReader = scoresCmd.ExecuteReader();

            while (scoresReader.Read())
            {
                var feature = scoresReader.GetString(0);
                var score = scoresReader.GetDouble(1);

                switch (feature.ToLower())
                {
                    case "cleanliness":
                        rating.CleanlinessScore = score;
                        break;
                    case "service":
                        rating.ServiceScore = score;
                        break;
                    case "location":
                        rating.LocationScore = score;
                        break;
                    case "comfort":
                        rating.ComfortScore = score;
                        break;
                    case "price":
                        rating.PriceScore = score;
                        break;
                }
            }

            // Calculate overall rating
            rating.OverallRating = (rating.CleanlinessScore + rating.ServiceScore +
                                   rating.LocationScore + rating.ComfortScore +
                                   rating.PriceScore) / 5.0;
        }

        public List<string> GetHotelsByCity(string city)
        {
            var hotels = new List<string>();

            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var tableRef = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var cmd = new OracleCommand($"SELECT hotel_name FROM {tableRef} WHERE city = :city ORDER BY hotel_name", conn);
                cmd.Parameters.Add(":city", OracleDbType.Varchar2).Value = city;

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    hotels.Add(reader.GetString(0));
                }
            }
            catch
            {
                // Handle error
            }

            return hotels;
        }

        public List<string> GetCities()
        {
            var cities = new List<string>();

            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var tableRef = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var cmd = new OracleCommand($"SELECT DISTINCT city FROM {tableRef} ORDER BY city", conn);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    cities.Add(reader.GetString(0));
                }
            }
            catch
            {
                // Handle error
            }

            return cities;
        }

        public List<string> GetActiveFeatures()
        {
            var features = new List<string>();

            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var featureTableRef = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";
                var cmd = new OracleCommand($"SELECT feature_name FROM {featureTableRef} WHERE is_active = 'Y' ORDER BY feature_name", conn);

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    features.Add(reader.GetString(0));
                }
            }
            catch
            {
                // Handle error
            }

            return features;
        }

        public List<HotelRating> AdvancedSearch(string city, Dictionary<string, double?> criteria)
        {
            var results = new List<HotelRating>();

            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var hotelTable = string.IsNullOrEmpty(CurrentSchema) ? "Hotel" : $"{CurrentSchema}.Hotel";
                var ratingTable = string.IsNullOrEmpty(CurrentSchema) ? "Rating" : $"{CurrentSchema}.Rating";
                var featureTable = string.IsNullOrEmpty(CurrentSchema) ? "Feature" : $"{CurrentSchema}.Feature";

                var whereClauses = new List<string> { "h.city = :city" };

                if (criteria["cleanliness"].HasValue)
                    whereClauses.Add("NVL(r_clean.score, 0) > :cleanliness");
                if (criteria["service"].HasValue)
                    whereClauses.Add("NVL(r_service.score, 0) > :service");
                if (criteria["location"].HasValue)
                    whereClauses.Add("NVL(r_location.score, 0) > :location");
                if (criteria["comfort"].HasValue)
                    whereClauses.Add("NVL(r_comfort.score, 0) > :comfort");
                if (criteria["price"].HasValue)
                    whereClauses.Add("NVL(r_price.score, 0) > :price");

                var sql = $@"
                    SELECT DISTINCT
                        h.hotel_name,
                        h.city,
                        h.country,
                        (NVL(r_clean.score, 0) + NVL(r_service.score, 0) + 
                         NVL(r_location.score, 0) + NVL(r_comfort.score, 0) + 
                         NVL(r_price.score, 0)) / 5.0 as overall_rating
                    FROM {hotelTable} h
                    LEFT JOIN {ratingTable} r_clean ON h.hotel_id = r_clean.hotel_id 
                        AND r_clean.feature_id = (SELECT feature_id FROM {featureTable} WHERE feature_name = 'Cleanliness')
                    LEFT JOIN {ratingTable} r_service ON h.hotel_id = r_service.hotel_id 
                        AND r_service.feature_id = (SELECT feature_id FROM {featureTable} WHERE feature_name = 'Service')
                    LEFT JOIN {ratingTable} r_location ON h.hotel_id = r_location.hotel_id 
                        AND r_location.feature_id = (SELECT feature_id FROM {featureTable} WHERE feature_name = 'Location')
                    LEFT JOIN {ratingTable} r_comfort ON h.hotel_id = r_comfort.hotel_id 
                        AND r_comfort.feature_id = (SELECT feature_id FROM {featureTable} WHERE feature_name = 'Comfort')
                    LEFT JOIN {ratingTable} r_price ON h.hotel_id = r_price.hotel_id 
                        AND r_price.feature_id = (SELECT feature_id FROM {featureTable} WHERE feature_name = 'Price')
                    WHERE {string.Join(" AND ", whereClauses)}
                    ORDER BY overall_rating DESC";

                var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(":city", OracleDbType.Varchar2).Value = city;

                if (criteria["cleanliness"].HasValue)
                    cmd.Parameters.Add(":cleanliness", OracleDbType.Double).Value = criteria["cleanliness"]!.Value;
                if (criteria["service"].HasValue)
                    cmd.Parameters.Add(":service", OracleDbType.Double).Value = criteria["service"]!.Value;
                if (criteria["location"].HasValue)
                    cmd.Parameters.Add(":location", OracleDbType.Double).Value = criteria["location"]!.Value;
                if (criteria["comfort"].HasValue)
                    cmd.Parameters.Add(":comfort", OracleDbType.Double).Value = criteria["comfort"]!.Value;
                if (criteria["price"].HasValue)
                    cmd.Parameters.Add(":price", OracleDbType.Double).Value = criteria["price"]!.Value;

                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    results.Add(new HotelRating
                    {
                        HotelName = reader.GetString(reader.GetOrdinal("hotel_name")),
                        City = reader.GetString(reader.GetOrdinal("city")),
                        Country = reader.GetString(reader.GetOrdinal("country")),
                        OverallRating = reader.GetDouble(reader.GetOrdinal("overall_rating"))
                    });
                }
            }
            catch
            {
                // Handle error
            }

            return results;
        }

        public bool AddFeature(string featureName)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var procRef = string.IsNullOrEmpty(CurrentSchema) ? "add_new_feature" : $"{CurrentSchema}.add_new_feature";
                var cmd = new OracleCommand(procRef, conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add("p_feature_name", OracleDbType.Varchar2).Value = featureName;
                cmd.Parameters.Add("p_description", OracleDbType.Varchar2).Value = DBNull.Value;
                cmd.ExecuteNonQuery();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool AddSeedWord(string featureName, string seedPhrase, int weight)
        {
            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var procRef = string.IsNullOrEmpty(CurrentSchema) ? "add_seed_word" : $"{CurrentSchema}.add_seed_word";
                var cmd = new OracleCommand(procRef, conn);
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add("p_feature_name", OracleDbType.Varchar2).Value = featureName;
                cmd.Parameters.Add("p_seed_phrase", OracleDbType.Varchar2).Value = seedPhrase;
                cmd.Parameters.Add("p_weight", OracleDbType.Int32).Value = weight;
                cmd.ExecuteNonQuery();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<(string schemaName, List<string> tables)> GetAllSchemasAndTables()
        {
            var schemaData = new List<(string schemaName, List<string> tables)>();

            using var conn = new OracleConnection(_connectionString);
            conn.Open();

            var cmd = new OracleCommand("SELECT DISTINCT OWNER FROM ALL_TABLES ORDER BY OWNER", conn);
            using var reader = cmd.ExecuteReader();
            var schemas = new List<string>();

            while (reader.Read())
            {
                schemas.Add(reader.GetString(0));
            }
            reader.Close();

            foreach (var schemaName in schemas)
            {
                var tables = new List<string>();
                var tableCmd = new OracleCommand($"SELECT table_name FROM all_tables WHERE owner = :owner ORDER BY table_name", conn);
                tableCmd.Parameters.Add(":owner", OracleDbType.Varchar2).Value = schemaName;

                try
                {
                    using var tableReader = tableCmd.ExecuteReader();

                    while (tableReader.Read())
                    {
                        tables.Add(tableReader.GetString(0));
                    }

                    if (tables.Count > 0)
                        schemaData.Add((schemaName, tables));
                }
                catch
                {
                    // Skip schemas we can't access
                }
            }

            return schemaData;
        }

        public List<object[]> GetTableData(string schema, string table, out List<string> columnNames)
        {
            columnNames = new List<string>();
            var rows = new List<object[]>();

            try
            {
                using var conn = new OracleConnection(_connectionString);
                conn.Open();

                var cmd = new OracleCommand($"SELECT * FROM {schema}.{table} WHERE ROWNUM <= 1000", conn);
                using var reader = cmd.ExecuteReader();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columnNames.Add(reader.GetName(i));
                }

                while (reader.Read())
                {
                    var row = new object[reader.FieldCount];
                    reader.GetValues(row);
                    rows.Add(row);
                }
            }
            catch
            {
                // Handle error
            }

            return rows;
        }
    }
}
