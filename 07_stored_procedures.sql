-- #############################################################################
-- # Filename: 07_stored_procedures.sql
-- # FINAL TUNED VERSION
-- # Implements the refined scoring model with a dampened intensity modifier
-- # to create better differentiation in scores.
-- #############################################################################

-- Drop existing procedures to ensure a clean slate
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE calculate_all_ratings'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_feature'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_seed_word'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_review'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/


CREATE OR REPLACE PROCEDURE calculate_all_ratings
IS
    -- This constant acts as a "tuning knob" for how much the word intensity
    -- (e.g., +3 for "excellent") affects the final score. A smaller number
    -- creates less of an impact and more differentiation at the top end.
    C_INTENSITY_FACTOR CONSTANT NUMBER := 0.2;

    -- Raw Calculation Variables
    v_total_weighted_score NUMBER;
    v_review_count NUMBER;
    v_phrase_count NUMBER;
    
    -- Mention Count Variables
    v_total_mentions NUMBER;
    v_pos_mentions NUMBER;

    -- Scoring Model Variables
    v_positivity_ratio NUMBER;
    v_baseline_stars NUMBER;
    v_intensity_modifier NUMBER;
    v_final_combined_score NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Beginning refined rating calculation...');
    
    DELETE FROM Rating;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Cleared existing data from Rating table.');

    FOR v_hotel_rec IN (SELECT hotel_id, hotel_name FROM Hotel)
    LOOP
        DBMS_OUTPUT.PUT_LINE('Processing Hotel: ' || v_hotel_rec.hotel_name);
        
        SELECT COUNT(*) INTO v_review_count FROM Review WHERE hotel_id = v_hotel_rec.hotel_id;

        FOR v_feature_rec IN (SELECT feature_id, feature_name FROM Feature WHERE is_active = 'Y')
        LOOP
            -- Reset all counters for each new hotel/feature combination
            v_total_weighted_score := 0;
            v_total_mentions := 0;
            v_pos_mentions := 0;

            FOR v_review_rec IN (SELECT review_text FROM Review WHERE hotel_id = v_hotel_rec.hotel_id)
            LOOP
                FOR v_seed_rec IN (SELECT seed_phrase, weight FROM Seed WHERE feature_id = v_feature_rec.feature_id)
                LOOP
                    v_phrase_count := REGEXP_COUNT(v_review_rec.review_text, v_seed_rec.seed_phrase, 1, 'i');
                    
                    IF v_phrase_count > 0 THEN
                        v_total_weighted_score := v_total_weighted_score + (v_phrase_count * v_seed_rec.weight);
                        v_total_mentions := v_total_mentions + v_phrase_count;
                        IF v_seed_rec.weight > 0 THEN
                            v_pos_mentions := v_pos_mentions + v_phrase_count;
                        END IF;
                    END IF;
                END LOOP;
            END LOOP;
            
            IF v_total_mentions = 0 THEN
                v_final_combined_score := 3; -- Default to neutral 3-star if no mentions
            ELSE
                -- 1. Calculate the Baseline Score based on positivity ratio
                v_positivity_ratio := v_pos_mentions / v_total_mentions;
                v_baseline_stars := (v_positivity_ratio * 4) + 1;

                -- 2. Calculate the DAMPENED Intensity Modifier
                IF v_review_count > 0 THEN
                    v_intensity_modifier := (v_total_weighted_score / v_review_count) * C_INTENSITY_FACTOR;
                ELSE
                    v_intensity_modifier := 0;
                END IF;

                -- 3. Combine them
                v_final_combined_score := v_baseline_stars + v_intensity_modifier;
            END IF;

            -- 4. Clamp the final score to the 1-5 range
            IF v_final_combined_score > 5 THEN
                v_final_combined_score := 5;
            ELSIF v_final_combined_score < 1 THEN
                v_final_combined_score := 1;
            END IF;

            INSERT INTO Rating (
                rating_id, hotel_id, feature_id, score,
                total_mentions, positive_mentions, negative_mentions
            ) VALUES (
                rating_seq.NEXTVAL, v_hotel_rec.hotel_id, v_feature_rec.feature_id, 
                ROUND(v_final_combined_score, 2),
                v_total_mentions, v_pos_mentions, (v_total_mentions - v_pos_mentions)
            );

        END LOOP;
        COMMIT;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('Refined rating calculation complete.');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('An error occurred: ' || SQLERRM);
        ROLLBACK;
END calculate_all_ratings;
/


-- Bonus Procedures (Unchanged)
CREATE OR REPLACE PROCEDURE add_new_feature (p_feature_name IN VARCHAR2, p_description IN VARCHAR2 DEFAULT NULL) AS BEGIN INSERT INTO Feature (feature_id, feature_name, description, is_active) VALUES (feature_seq.NEXTVAL, p_feature_name, p_description, 'Y'); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_seed_word (p_feature_name IN VARCHAR2, p_seed_phrase IN VARCHAR2, p_weight IN NUMBER) AS v_feature_id NUMBER; BEGIN SELECT feature_id INTO v_feature_id FROM Feature WHERE feature_name = p_feature_name; INSERT INTO Seed (seed_id, feature_id, seed_phrase, weight) VALUES (seed_seq.NEXTVAL, v_feature_id, p_seed_phrase, p_weight); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_review (p_hotel_name IN VARCHAR2, p_review_text IN CLOB, p_reviewer_name IN VARCHAR2, p_review_date IN DATE, p_overall_rating IN NUMBER) AS v_hotel_id NUMBER; BEGIN SELECT hotel_id INTO v_hotel_id FROM Hotel WHERE hotel_name = p_hotel_name; INSERT INTO Review(review_id, hotel_id, review_text, reviewer_name, review_date, overall_rating) VALUES (review_seq.NEXTVAL, v_hotel_id, p_review_text, p_reviewer_name, p_review_date, p_overall_rating); COMMIT; END;
/


SELECT 'Stored procedures created successfully' AS status FROM dual;
