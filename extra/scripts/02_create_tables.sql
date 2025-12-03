-- ============================================
-- Hotel Rating System - Table Creation Script
-- ============================================

-- Table: Hotel
CREATE TABLE Hotel (
    hotel_id NUMBER PRIMARY KEY,
    hotel_name VARCHAR2(200) NOT NULL,
    address VARCHAR2(500),
    city VARCHAR2(100),
    country VARCHAR2(100),
    star_rating NUMBER(2,1) CHECK (star_rating BETWEEN 1 AND 5),
    created_date DATE DEFAULT SYSDATE,
    CONSTRAINT uk_hotel_name_city UNIQUE (hotel_name, city)
);

-- Table: Feature
CREATE TABLE Feature (
    feature_id NUMBER PRIMARY KEY,
    feature_name VARCHAR2(100) NOT NULL UNIQUE,
    description VARCHAR2(500),
    is_active CHAR(1) DEFAULT 'Y' CHECK (is_active IN ('Y', 'N')),
    created_date DATE DEFAULT SYSDATE
);

-- Table: Seed
CREATE TABLE Seed (
    seed_id NUMBER PRIMARY KEY,
    feature_id NUMBER NOT NULL,
    seed_phrase VARCHAR2(200) NOT NULL,
    weight NUMBER(2) NOT NULL,
    created_date DATE DEFAULT SYSDATE,
    CONSTRAINT fk_seed_feature FOREIGN KEY (feature_id) 
        REFERENCES Feature(feature_id) ON DELETE CASCADE,
    CONSTRAINT uk_seed_phrase_feature UNIQUE (feature_id, seed_phrase)
);

-- Table: Review
CREATE TABLE Review (
    review_id NUMBER PRIMARY KEY,
    hotel_id NUMBER NOT NULL,
    review_text CLOB NOT NULL,
    reviewer_name VARCHAR2(200),
    review_date DATE,
    overall_rating NUMBER(2,1) CHECK (overall_rating BETWEEN 1 AND 5),
    created_date DATE DEFAULT SYSDATE,
    CONSTRAINT fk_review_hotel FOREIGN KEY (hotel_id) 
        REFERENCES Hotel(hotel_id) ON DELETE CASCADE
);

-- Table: Rating (Junction/Middle table)
CREATE TABLE Rating (
    rating_id NUMBER PRIMARY KEY,
    hotel_id NUMBER NOT NULL,
    feature_id NUMBER NOT NULL,
    score NUMBER(5,2) NOT NULL,
    total_mentions NUMBER DEFAULT 0,
    positive_mentions NUMBER DEFAULT 0,
    negative_mentions NUMBER DEFAULT 0,
    last_updated DATE DEFAULT SYSDATE,
    CONSTRAINT fk_rating_hotel FOREIGN KEY (hotel_id) 
        REFERENCES Hotel(hotel_id) ON DELETE CASCADE,
    CONSTRAINT fk_rating_feature FOREIGN KEY (feature_id) 
        REFERENCES Feature(feature_id) ON DELETE CASCADE,
    CONSTRAINT uk_rating_hotel_feature UNIQUE (hotel_id, feature_id)
);

-- Add comments for documentation
COMMENT ON TABLE Hotel IS 'Stores hotel master data';
COMMENT ON TABLE Feature IS 'Stores rating features/categories';
COMMENT ON TABLE Seed IS 'Stores seed words/phrases for sentiment analysis';
COMMENT ON TABLE Review IS 'Stores customer reviews';
COMMENT ON TABLE Rating IS 'Stores calculated feature ratings for each hotel';

SELECT 'All tables created successfully' AS status FROM dual;
