-- Drop the view if it exists to ensure a clean create/replace
BEGIN
   EXECUTE IMMEDIATE 'DROP VIEW vw_seed_word_analysis';
EXCEPTION
   WHEN OTHERS THEN
      IF SQLCODE != -2289 AND SQLCODE != -942 THEN -- ORA-2289: sequence does not exist; ORA-942: table or view does not exist
         RAISE;
      END IF;
END;
/


CREATE OR REPLACE VIEW vw_seed_word_analysis AS
WITH SeedMentions AS (
    -- Step 1: Pre-calculate the total mentions for each seed phrase using a CTE.
    SELECT
        s.seed_id,
        SUM(REGEXP_COUNT(r.review_text, s.seed_phrase, 1, 'i')) AS total_mentions_calc
    FROM
        Seed s, Review r
    GROUP BY
        s.seed_id, s.seed_phrase
)
-- Step 2: Join the pre-calculated mentions and perform the final calculations.
SELECT
    f.feature_name,
    s.seed_phrase,
    s.weight AS assigned_weight,
    NVL(sm.total_mentions_calc, 0) AS total_mentions,
    
    -- This subquery is fine as it's self-contained
    (SELECT AVG(r.overall_rating) FROM Review r WHERE REGEXP_COUNT(r.review_text, s.seed_phrase, 1, 'i') > 0) AS avg_user_rating_when_present,
    
    -- Now, the multiplication is done on a simple column, which is valid.
    NVL(sm.total_mentions_calc, 0) * s.weight AS total_impact_score
FROM
    Seed s
JOIN
    Feature f ON s.feature_id = f.feature_id
LEFT JOIN
    SeedMentions sm ON s.seed_id = sm.seed_id;
/

SELECT 'Analysis views created successfully' AS status FROM dual;

