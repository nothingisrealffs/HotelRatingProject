-- #############################################################################
-- # PRE-FLIGHT CLEANUP SCRIPT
-- # Truncate tables to ensure a clean slate before inserting new data.
-- # This prevents ORA-00001 unique constraint violations.
-- #############################################################################
PROMPT Truncating target tables...
TRUNCATE TABLE Rating;
TRUNCATE TABLE Seed;
TRUNCATE TABLE Review;
TRUNCATE TABLE Feature;
TRUNCATE TABLE Hotel;
PROMPT Tables truncated.
COMMIT;

-- #############################################################################
-- # DATA INSERTION SCRIPT
-- #############################################################################

-- 1. Hotels
PROMPT Inserting data into Hotel table...
INSERT INTO Hotel (hotel_id, hotel_name, address, city, country, star_rating)
SELECT
    hotel_seq.NEXTVAL,
    hotel_name,
    address,
    city,
    country,
    -- CORRECTED LOGIC: Handle 'NA' strings from the CSV
    CASE
        WHEN star_rating IS NULL OR star_rating = 'NA'
        THEN NULL
        ELSE TO_NUMBER(star_rating)
    END
FROM ext_hotels;

-- 2. Features
PROMPT Inserting data into Feature table...
INSERT INTO Feature (feature_id, feature_name, description, is_active)
SELECT feature_seq.NEXTVAL, feature_name, description, is_active
FROM ext_features;

-- 3. Seeds
PROMPT Inserting data into Seed table...
INSERT INTO Seed (seed_id, feature_id, seed_phrase, weight)
SELECT seed_seq.NEXTVAL, f.feature_id, es.seed_phrase, es.weight
FROM ext_seeds es
JOIN Feature f ON f.feature_name = es.feature_name;

-- 4. Reviews
PROMPT Inserting data into Review table...
INSERT INTO Review (review_id, hotel_id, review_text, reviewer_name, review_date, overall_rating)
SELECT
    review_seq.NEXTVAL,
    h.hotel_id,
    er.review_text,
    er.reviewer_name,
    -- Date parsing logic (your original logic was good)
    CASE
        WHEN REGEXP_LIKE(er.review_date, '^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2}$')
        THEN TO_DATE(er.review_date, 'MM/DD/YY')
        WHEN REGEXP_LIKE(er.review_date, '^[0-9]{4}-[0-9]{2}-[0-9]{2}$')
        THEN TO_DATE(er.review_date, 'YYYY-MM-DD')
        ELSE NULL
    END,
    -- CORRECTED LOGIC: Handle 'NA' strings from the CSV
    CASE
        WHEN er.overall_rating IS NULL OR er.overall_rating = 'NA'
        THEN NULL
        ELSE TO_NUMBER(er.overall_rating)
    END
FROM ext_reviews er
JOIN Hotel h ON h.hotel_name = er.hotel_name;

PROMPT All data insertion complete.
COMMIT;

SELECT 'Data Loading Script Finished Successfully.' AS status FROM dual;
