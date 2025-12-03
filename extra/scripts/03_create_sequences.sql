-- ============================================
-- Sequences for Auto-Increment IDs
-- ============================================

CREATE SEQUENCE hotel_seq
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE SEQUENCE review_seq
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE SEQUENCE feature_seq
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE SEQUENCE seed_seq
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

CREATE SEQUENCE rating_seq
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;

SELECT 'All sequences created successfully' AS status FROM dual;
