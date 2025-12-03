-- ============================================
-- Useful Views for Querying
-- ============================================

-- View: Hotel Ratings Summary
CREATE OR REPLACE VIEW vw_hotel_ratings AS
SELECT 
    h.hotel_id,
    h.hotel_name,
    h.city,
    h.country,
    f.feature_name,
    r.score,
    r.total_mentions,
    r.positive_mentions,
    r.negative_mentions,
    r.last_updated
FROM Hotel h
JOIN Rating r ON h.hotel_id = r.hotel_id
JOIN Feature f ON r.feature_id = f.feature_id
ORDER BY h.hotel_name, f.feature_name;

-- View: Hotel Overall Rating
CREATE OR REPLACE VIEW vw_hotel_overall AS
SELECT 
    h.hotel_id,
    h.hotel_name,
    h.city,
    h.country,
    ROUND(AVG(r.score), 2) AS avg_score,
    COUNT(DISTINCT f.feature_id) AS features_rated,
    COUNT(DISTINCT rv.review_id) AS total_reviews
FROM Hotel h
LEFT JOIN Rating r ON h.hotel_id = r.hotel_id
LEFT JOIN Feature f ON r.feature_id = f.feature_id
LEFT JOIN Review rv ON h.hotel_id = rv.hotel_id
GROUP BY h.hotel_id, h.hotel_name, h.city, h.country;

-- View: Feature Seed Words
CREATE OR REPLACE VIEW vw_feature_seeds AS
SELECT 
    f.feature_name,
    s.seed_phrase,
    s.weight,
    CASE s.weight 
        WHEN 1 THEN 'Positive'
        WHEN -1 THEN 'Negative'
    END AS sentiment
FROM Feature f
JOIN Seed s ON f.feature_id = s.feature_id
ORDER BY f.feature_name, s.weight DESC, s.seed_phrase;

SELECT 'All views created successfully' AS status FROM dual;
