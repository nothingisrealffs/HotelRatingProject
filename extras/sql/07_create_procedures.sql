-- #############################################################################
-- # Filename: 07_stored_procedures.sql
-- # FINAL TUNED VERSION
-- # Implements DIMINISHING RETURNS for repeated phrases using logarithmic scaling.
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
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE calculate_all_ratingsv2'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/

CREATE OR REPLACE PROCEDURE calculate_all_ratings
IS
    -- This factor is increased to give the dampened modifier more influence.
    C_INTENSITY_FACTOR CONSTANT NUMBER := 0.8;

    v_total_weighted_score NUMBER;
    v_review_count NUMBER;
    v_phrase_count NUMBER;
    v_total_mentions NUMBER;
    v_pos_mentions NUMBER;
    v_positivity_ratio NUMBER;
    v_baseline_stars NUMBER;
    v_intensity_modifier NUMBER;
    v_final_combined_score NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Beginning refined rating calculation (with Diminishing Returns)...');
    
    DELETE FROM Rating;
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Cleared existing data from Rating table.');

    FOR v_hotel_rec IN (SELECT hotel_id, hotel_name FROM Hotel)
    LOOP
        SELECT COUNT(*) INTO v_review_count FROM Review WHERE hotel_id = v_hotel_rec.hotel_id;

        FOR v_feature_rec IN (SELECT feature_id FROM Feature WHERE is_active = 'Y')
        LOOP
            v_total_weighted_score := 0;
            v_total_mentions := 0;
            v_pos_mentions := 0;

            FOR v_review_rec IN (SELECT review_text FROM Review WHERE hotel_id = v_hotel_rec.hotel_id)
            LOOP
                FOR v_seed_rec IN (SELECT seed_phrase, weight FROM Seed WHERE feature_id = v_feature_rec.feature_id)
                LOOP
                    v_phrase_count := REGEXP_COUNT(v_review_rec.review_text, v_seed_rec.seed_phrase, 1, 'i');
                    
                    IF v_phrase_count > 0 THEN
                        -- *** NEW LOGIC: DIMINISHING RETURNS ***
                        -- Instead of a linear (count * weight), we use a logarithmic
                        -- function so that repeated mentions have less and less impact.
                        v_total_weighted_score := v_total_weighted_score + (LN(v_phrase_count + 1) * v_seed_rec.weight);
                        
                        -- Mention counts should still be the raw, linear count.
                        v_total_mentions := v_total_mentions + v_phrase_count;
                        IF v_seed_rec.weight > 0 THEN
                            v_pos_mentions := v_pos_mentions + v_phrase_count;
                        END IF;
                    END IF;
                END LOOP;
            END LOOP;
            
            IF v_total_mentions = 0 THEN
                v_final_combined_score := 3;
            ELSE
                v_positivity_ratio := v_pos_mentions / v_total_mentions;
                v_baseline_stars := (v_positivity_ratio * 4) + 1;

                IF v_review_count > 0 THEN
                    v_intensity_modifier := (v_total_weighted_score / v_review_count) * C_INTENSITY_FACTOR;
                ELSE
                    v_intensity_modifier := 0;
                END IF;

                v_final_combined_score := v_baseline_stars + v_intensity_modifier;
            END IF;

            IF v_final_combined_score > 5 THEN v_final_combined_score := 5;
            ELSIF v_final_combined_score < 1 THEN v_final_combined_score := 1;
            END IF;

            INSERT INTO Rating (rating_id, hotel_id, feature_id, score, total_mentions, positive_mentions, negative_mentions) 
            VALUES (rating_seq.NEXTVAL, v_hotel_rec.hotel_id, v_feature_rec.feature_id, ROUND(v_final_combined_score, 2),
                    v_total_mentions, v_pos_mentions, (v_total_mentions - v_pos_mentions));
        END LOOP;
        COMMIT;
    END LOOP;
    DBMS_OUTPUT.PUT_LINE('Rating calculation complete.');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('An error occurred: ' || SQLERRM);
        ROLLBACK;
END calculate_all_ratings;
/

-- Replacement: calculate_all_ratings_v2
-- Counts DISTINCT review mentions per seed (one mention per review),
-- uses LN(distinct_review_mentions + 1) for diminishing returns across reviews,
-- maps baseline so neutral ≈ 2.5, and caps final score to [1,5].
-- Meant to be used in the same schema as your previous procedures.
CREATE OR REPLACE PROCEDURE calculate_all_ratings_v2
IS
    -- Intensity multiplier (tuneable)
    C_INTENSITY_FACTOR CONSTANT NUMBER := 0.8;

    v_total_weighted_score NUMBER;
    v_review_count NUMBER;
    v_total_mentions NUMBER;
    v_pos_mentions NUMBER;
    v_neg_mentions NUMBER;
    v_positivity_ratio NUMBER;
    v_baseline_stars NUMBER;
    v_intensity_modifier NUMBER;
    v_final_combined_score NUMBER;

    v_seed_feature_id NUMBER;
    v_seed_phrase VARCHAR2(4000);
    v_seed_weight NUMBER;

    v_distinct_review_mentions NUMBER;
BEGIN
    DBMS_OUTPUT.PUT_LINE('Beginning refined rating calculation (v2) - distinct review mentions per seed...');

    -- clear the Rating table
    DELETE FROM Rating;
    COMMIT;

    -- iterate hotels
    FOR v_hotel_rec IN (SELECT hotel_id, hotel_name FROM Hotel)
    LOOP
        -- how many reviews for this hotel (for scaling)
        SELECT COUNT(*) INTO v_review_count FROM Review WHERE hotel_id = v_hotel_rec.hotel_id;

        -- iterate active features
        FOR v_feature_rec IN (SELECT feature_id FROM Feature WHERE is_active = 'Y')
        LOOP
            v_total_weighted_score := 0;
            v_total_mentions := 0;
            v_pos_mentions := 0;
            v_neg_mentions := 0;

            -- for each seed word for this feature
            FOR v_seed_rec IN (SELECT seed_id, seed_phrase, weight FROM Seed WHERE feature_id = v_feature_rec.feature_id)
            LOOP
                v_seed_phrase := v_seed_rec.seed_phrase;
                v_seed_weight := NVL(v_seed_rec.weight, 0);

                /*
                  Count DISTINCT reviews that mention the seed phrase at least once.
                  This avoids counting repeated occurrences inside the same review.
                  Use REGEXP_COUNT(...) > 0 as boolean per review and sum those 1s.
                */
                SELECT COUNT(*) INTO v_distinct_review_mentions
                FROM Review r
                WHERE r.hotel_id = v_hotel_rec.hotel_id
                  AND REGEXP_COUNT(r.review_text, v_seed_phrase, 1, 'i') > 0;

                IF v_distinct_review_mentions > 0 THEN
                    -- accumulate weighted intensity using diminishing returns across reviews:
                    -- LN(distinct_review_mentions + 1) gives diminishing marginal effect as number of reviews mentioning grows.
                    v_total_weighted_score := v_total_weighted_score + (LN(v_distinct_review_mentions + 1) * v_seed_weight);

                    -- accumulate distinct mention counts (one per review)
                    v_total_mentions := v_total_mentions + v_distinct_review_mentions;

                    IF v_seed_weight > 0 THEN
                        v_pos_mentions := v_pos_mentions + v_distinct_review_mentions;
                    ELSIF v_seed_weight < 0 THEN
                        v_neg_mentions := v_neg_mentions + v_distinct_review_mentions;
                    END IF;
                END IF;
            END LOOP; -- seeds loop

            -- if we saw no mentions at all for this feature on this hotel, set neutral baseline
            IF v_total_mentions = 0 THEN
                v_final_combined_score := 2.5; -- neutral when no information
            ELSE
                -- positivity ratio from distinct-review counts:
                v_positivity_ratio := CASE WHEN v_total_mentions > 0 THEN v_pos_mentions / v_total_mentions ELSE 0.0 END;

                -- Map positivity ratio to baseline stars where 0 => 0, 0.5 => 2.5, 1 => 5
                -- but shift and scale to put neutral at 2.5 and let negative skew reduce below neutral.
                v_baseline_stars := (v_positivity_ratio * 5.0);  -- 0 -> 0, 1 -> 5
                -- Optionally shift toward 2.5 neutral center if you want: v_baseline_stars := (v_positivity_ratio - 0.5) * 5 + 2.5;

                -- Intensity modifier scaled per-review to avoid huge boosts for hotels with many reviews:
                IF v_review_count > 0 THEN
                    -- divide by log( review_count + 1 ) to dampen effect for extremely large review counts
                    v_intensity_modifier := (v_total_weighted_score / GREATEST(1, LN(v_review_count + 1))) * C_INTENSITY_FACTOR;
                ELSE
                    v_intensity_modifier := 0;
                END IF;

                v_final_combined_score := v_baseline_stars + v_intensity_modifier;
            END IF;

            -- clamp to database rating bounds (1..5)
            IF v_final_combined_score > 5 THEN
                v_final_combined_score := 5;
            ELSIF v_final_combined_score < 1 THEN
                v_final_combined_score := 1;
            END IF;

            -- Insert rating for this hotel-feature with distinct mention counts
            INSERT INTO Rating (rating_id, hotel_id, feature_id, score, total_mentions, positive_mentions, negative_mentions)
            VALUES (rating_seq.NEXTVAL, v_hotel_rec.hotel_id, v_feature_rec.feature_id,
                    ROUND(v_final_combined_score, 2),
                    v_total_mentions, v_pos_mentions, v_neg_mentions);
        END LOOP; -- feature loop

        COMMIT;
    END LOOP; -- hotel loop

    DBMS_OUTPUT.PUT_LINE('calculate_all_ratings_v2 - complete.');

EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error in calculate_all_ratings_v2: ' || SQLERRM);
        ROLLBACK;
END calculate_all_ratings_v2;
/

-- Bonus Procedures (Unchanged)
CREATE OR REPLACE PROCEDURE add_new_feature (p_feature_name IN VARCHAR2, p_description IN VARCHAR2 DEFAULT NULL) AS BEGIN INSERT INTO Feature (feature_id, feature_name, description, is_active) VALUES (feature_seq.NEXTVAL, p_feature_name, p_description, 'Y'); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_seed_word (p_feature_name IN VARCHAR2, p_seed_phrase IN VARCHAR2, p_weight IN NUMBER) AS v_feature_id NUMBER; BEGIN SELECT feature_id INTO v_feature_id FROM Feature WHERE feature_name = p_feature_name; INSERT INTO Seed (seed_id, feature_id, seed_phrase, weight) VALUES (seed_seq.NEXTVAL, v_feature_id, p_seed_phrase, p_weight); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_review (p_hotel_name IN VARCHAR2, p_review_text IN CLOB, p_reviewer_name IN VARCHAR2, p_review_date IN DATE, p_overall_rating IN NUMBER) AS v_hotel_id NUMBER; BEGIN SELECT hotel_id INTO v_hotel_id FROM Hotel WHERE hotel_name = p_hotel_name; INSERT INTO Review(review_id, hotel_id, review_text, reviewer_name, review_date, overall_rating) VALUES (review_seq.NEXTVAL, v_hotel_id, p_review_text, p_reviewer_name, p_review_date, p_overall_rating); COMMIT; END;
/


SELECT 'Stored procedures created successfully' AS status FROM dual;
