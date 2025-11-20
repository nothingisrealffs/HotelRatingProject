-- ============================================
-- Indexes for Performance Optimization
-- ============================================

-- Indexes on Foreign Keys
CREATE INDEX idx_seed_feature_id ON Seed(feature_id);
CREATE INDEX idx_review_hotel_id ON Review(hotel_id);
CREATE INDEX idx_rating_hotel_id ON Rating(hotel_id);
CREATE INDEX idx_rating_feature_id ON Rating(feature_id);

-- Indexes for common searches
CREATE INDEX idx_hotel_city ON Hotel(city);
CREATE INDEX idx_review_date ON Review(review_date);
CREATE INDEX idx_seed_phrase ON Seed(UPPER(seed_phrase));

-- Full-text search on review text (optional, for Oracle Text)
-- CREATE INDEX idx_review_text ON Review(review_text) INDEXTYPE IS CTXSYS.CONTEXT;

SELECT 'All indexes created successfully' AS status FROM dual;
