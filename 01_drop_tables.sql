-- Drop tables in reverse order of dependencies
-- Run this to reset the database

DROP TABLE Rating CASCADE CONSTRAINTS;
DROP TABLE Review CASCADE CONSTRAINTS;
DROP TABLE Seed CASCADE CONSTRAINTS;
DROP TABLE Feature CASCADE CONSTRAINTS;
DROP TABLE Hotel CASCADE CONSTRAINTS;

-- Drop sequences
DROP SEQUENCE hotel_seq;
DROP SEQUENCE review_seq;
DROP SEQUENCE feature_seq;
DROP SEQUENCE seed_seq;
DROP SEQUENCE rating_seq;

-- Confirmation message
SELECT 'All tables and sequences dropped successfully' AS status FROM dual;
