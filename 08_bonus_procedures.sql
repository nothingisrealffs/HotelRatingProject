-- ============================================
-- BONUS: CRUD Procedures for Dynamic Updates
-- ============================================

-- Procedure: Add new feature
CREATE OR REPLACE PROCEDURE add_new_feature(
    p_feature_name IN VARCHAR2,
    p_description IN VARCHAR2 DEFAULT NULL
)
IS
BEGIN
    INSERT INTO Feature (feature_id, feature_name, description)
    VALUES (feature_seq.NEXTVAL, p_feature_name, p_description);
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Feature "' || p_feature_name || '" added successfully');
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        DBMS_OUTPUT.PUT_LINE('Error: Feature "' || p_feature_name || '" already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error adding feature: ' || SQLERRM);
        RAISE;
END;
/

-- Procedure: Add new seed word
CREATE OR REPLACE PROCEDURE add_seed_word(
    p_feature_name IN VARCHAR2,
    p_seed_phrase IN VARCHAR2,
    p_weight IN NUMBER
)
IS
    v_feature_id NUMBER;
BEGIN
    -- Get feature_id
    SELECT feature_id INTO v_feature_id
    FROM Feature
    WHERE feature_name = p_feature_name;
    
    -- Validate weight
    IF p_weight NOT IN (-1, 1) THEN
        RAISE_APPLICATION_ERROR(-20001, 'Weight must be -1 or 1');
    END IF;
    
    INSERT INTO Seed (seed_id, feature_id, seed_phrase, weight)
    VALUES (seed_seq.NEXTVAL, v_feature_id, p_seed_phrase, p_weight);
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Seed word "' || p_seed_phrase || '" added for feature "' || p_feature_name || '"');
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('Error: Feature "' || p_feature_name || '" not found');
    WHEN DUP_VAL_ON_INDEX THEN
        DBMS_OUTPUT.PUT_LINE('Error: Seed phrase already exists for this feature');
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error adding seed word: ' || SQLERRM);
        RAISE;
END;
/

-- Procedure: Add new review
CREATE OR REPLACE PROCEDURE add_new_review(
    p_hotel_name IN VARCHAR2,
    p_review_text IN CLOB,
    p_reviewer_name IN VARCHAR2 DEFAULT NULL,
    p_review_date IN DATE DEFAULT SYSDATE,
    p_overall_rating IN NUMBER DEFAULT NULL
)
IS
    v_hotel_id NUMBER;
BEGIN
    -- Get hotel_id
    SELECT hotel_id INTO v_hotel_id
    FROM Hotel
    WHERE hotel_name = p_hotel_name;
    
    INSERT INTO Review (review_id, hotel_id, review_text, reviewer_name, review_date, overall_rating)
    VALUES (review_seq.NEXTVAL, v_hotel_id, p_review_text, p_reviewer_name, p_review_date, p_overall_rating);
    
    -- Recalculate ratings for this hotel
    calculate_hotel_ratings(v_hotel_id);
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Review added and ratings recalculated for "' || p_hotel_name || '"');
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('Error: Hotel "' || p_hotel_name || '" not found');
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error adding review: ' || SQLERRM);
        RAISE;
END;
/

-- Procedure: Add new hotel with review
CREATE OR REPLACE PROCEDURE add_new_hotel_with_review(
    p_hotel_name IN VARCHAR2,
    p_address IN VARCHAR2 DEFAULT NULL,
    p_city IN VARCHAR2 DEFAULT NULL,
    p_country IN VARCHAR2 DEFAULT NULL,
    p_star_rating IN NUMBER DEFAULT NULL,
    p_review_text IN CLOB DEFAULT NULL,
    p_reviewer_name IN VARCHAR2 DEFAULT NULL
)
IS
    v_hotel_id NUMBER;
BEGIN
    -- Insert hotel
    INSERT INTO Hotel (hotel_id, hotel_name, address, city, country, star_rating)
    VALUES (hotel_seq.NEXTVAL, p_hotel_name, p_address, p_city, p_country, p_star_rating)
    RETURNING hotel_id INTO v_hotel_id;
    
    -- Insert review if provided
    IF p_review_text IS NOT NULL THEN
        INSERT INTO Review (review_id, hotel_id, review_text, reviewer_name, review_date)
        VALUES (review_seq.NEXTVAL, v_hotel_id, p_review_text, p_reviewer_name, SYSDATE);
        
        -- Calculate ratings
        calculate_hotel_ratings(v_hotel_id);
    END IF;
    
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Hotel "' || p_hotel_name || '" added with ID: ' || v_hotel_id);
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        DBMS_OUTPUT.PUT_LINE('Error: Hotel with same name in same city already exists');
    WHEN OTHERS THEN
        ROLLBACK;
        DBMS_OUTPUT.PUT_LINE('Error adding hotel: ' || SQLERRM);
        RAISE;
END;
/

SELECT 'Bonus CRUD procedures created successfully' AS status FROM dual;
