
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE calculate_all_ratings'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_feature'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_seed_word'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE add_new_review'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
BEGIN EXECUTE IMMEDIATE 'DROP PROCEDURE calc_ratings'; EXCEPTION WHEN OTHERS THEN IF SQLCODE != -4043 THEN RAISE; END IF; END;
/
CREATE OR REPLACE PROCEDURE CALC_RATINGS
IS

    A_bound CONSTANT NUMBER := 0.5;   -- controls squash sensitivity
    K_prior CONSTANT NUMBER := 20;    -- shrinkage strength toward neutral (2.5)

    CURSOR c_hotels IS
        SELECT hotel_id, hotel_name FROM Hotel;

    CURSOR c_features IS
        SELECT feature_id FROM Feature WHERE is_active = 'Y';

    v_n_reviews_with_hits NUMBER;
    v_avg_sentiment       NUMBER;
    v_pos_reviews         NUMBER;
    v_neg_reviews         NUMBER;
    v_total_reviews_for_hotel NUMBER;

    v_mapped_mean NUMBER;
    v_final_score NUMBER;

BEGIN
    DBMS_OUTPUT.PUT_LINE('CALC_RATINGS: start');

    DELETE FROM Rating;
    COMMIT;

    FOR v_hotel_rec IN c_hotels LOOP

        SELECT COUNT(*) INTO v_total_reviews_for_hotel
        FROM Review
        WHERE hotel_id = v_hotel_rec.hotel_id;

        -- Loop through active features
        FOR v_feature_rec IN c_features LOOP

            /*
              Build one aggregated sentiment per review:
              review_sentiment = SUM(seed.weight) for all seeds of this feature that match that review.
              Each review contributes at most once per feature.
            */
            BEGIN
                SELECT COUNT(*) AS n_reviews,
                       NVL(AVG(review_sentiment),0) AS avg_sentiment,
                       SUM(CASE WHEN review_sentiment > 0 THEN 1 ELSE 0 END) AS pos_reviews,
                       SUM(CASE WHEN review_sentiment < 0 THEN 1 ELSE 0 END) AS neg_reviews
                INTO v_n_reviews_with_hits, v_avg_sentiment, v_pos_reviews, v_neg_reviews
                FROM (
                    SELECT r.review_id,
                           SUM(NVL(s.weight,0)) AS review_sentiment
                    FROM Review r
                    JOIN Seed s
                      ON s.feature_id = v_feature_rec.feature_id
                    WHERE r.hotel_id = v_hotel_rec.hotel_id
                      AND REGEXP_COUNT(r.review_text, s.seed_phrase, 1, 'i') > 0
                    GROUP BY r.review_id
                );

            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    v_n_reviews_with_hits := 0;
                    v_avg_sentiment := 0;
                    v_pos_reviews := 0;
                    v_neg_reviews := 0;
            END;

            -- Compute final score
            IF v_n_reviews_with_hits = 0 THEN
                v_final_score := 2.5; -- Forgot to adjust this
            ELSE

                v_mapped_mean := 3 + 2 * (v_avg_sentiment / (A_bound + ABS(v_avg_sentiment)));

                v_final_score := (K_prior * 3 + v_n_reviews_with_hits * v_mapped_mean)
                                 / (K_prior + v_n_reviews_with_hits);
            END IF;

            -- Clamp into [1,5]
            IF v_final_score > 5 THEN
                v_final_score := 5;
            ELSIF v_final_score < 1 THEN
                v_final_score := 1;
            END IF;

            -- Insert into Rating preserving same columns
            INSERT INTO Rating (rating_id, hotel_id, feature_id, score, total_mentions, positive_mentions, negative_mentions)
            VALUES (rating_seq.NEXTVAL,
                    v_hotel_rec.hotel_id,
                    v_feature_rec.feature_id,
                    ROUND(v_final_score, 2),
                    v_n_reviews_with_hits,
                    v_pos_reviews,
                    v_neg_reviews);

        END LOOP; -- feature loop

        COMMIT;

    END LOOP; 
    DBMS_OUTPUT.PUT_LINE('CALC_RATINGS: complete.');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Error in CALC_RATINGS: ' || SQLERRM);
        ROLLBACK;
        RAISE;
END CALC_RATINGS;
/

CREATE OR REPLACE PROCEDURE add_new_feature (p_feature_name IN VARCHAR2, p_description IN VARCHAR2 DEFAULT NULL) AS BEGIN INSERT INTO Feature (feature_id, feature_name, description, is_active) VALUES (feature_seq.NEXTVAL, p_feature_name, p_description, 'Y'); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_seed_word (p_feature_name IN VARCHAR2, p_seed_phrase IN VARCHAR2, p_weight IN NUMBER) AS v_feature_id NUMBER; BEGIN SELECT feature_id INTO v_feature_id FROM Feature WHERE feature_name = p_feature_name; INSERT INTO Seed (seed_id, feature_id, seed_phrase, weight) VALUES (seed_seq.NEXTVAL, v_feature_id, p_seed_phrase, p_weight); COMMIT; END;
/
CREATE OR REPLACE PROCEDURE add_new_review (p_hotel_name IN VARCHAR2, p_review_text IN CLOB, p_reviewer_name IN VARCHAR2, p_review_date IN DATE, p_overall_rating IN NUMBER) AS v_hotel_id NUMBER; BEGIN SELECT hotel_id INTO v_hotel_id FROM Hotel WHERE hotel_name = p_hotel_name; INSERT INTO Review(review_id, hotel_id, review_text, reviewer_name, review_date, overall_rating) VALUES (review_seq.NEXTVAL, v_hotel_id, p_review_text, p_reviewer_name, p_review_date, p_overall_rating); COMMIT; END;
/


SELECT 'Stored procedures created successfully' AS status FROM dual;
