-- =============================================================================
-- Script: Merge YouTube "Creator Critical" sections into one
-- Target: YouTube CSO IQA Evaluation Form (ScoringMethod = SectionAutoFail)
-- Merges:
--   "Creator Critical — Effectiveness"
--   "Creator Critical — Effort"
--   "Creator Critical — Engagement"
-- Into a single section named "Creator Critical"
--
-- ⚠️  DATABASE COMPATIBILITY ⚠️
--   This script is written for SQLite (the default database).
--   If you are using SQL Server, replace every occurrence of:
--       LIMIT 1   →   (used inside sub-selects, not supported in SQL Server)
--   with the equivalent SQL Server pattern using TOP 1 in a sub-select, e.g.:
--       SELECT TOP 1 Id FROM …   instead of   SELECT Id FROM … LIMIT 1
--
-- Run this ONCE on an existing database that still has the 3 split sections.
-- The current seed creates the merged section automatically for fresh installs.
-- =============================================================================

-- ── STEP 1: Find the target form ──────────────────────────────────────────────
-- Identify the YouTube IQA form (adjust the name filter if your instance renamed it)
-- We use a sub-select so this is portable across SQLite and SQL Server.

-- ── STEP 2: Locate the three source sections ──────────────────────────────────
-- We keep the Effectiveness section as the merged "Creator Critical" section
-- (lowest Order value → appears first), then reassign Effort/Engagement fields
-- to it, and finally delete the now-empty Effort and Engagement sections.

-- Reassign all FormFields that belong to "Creator Critical — Effort"
-- to the "Creator Critical — Effectiveness" section, adjusting Order values
-- so they continue after the existing Effectiveness fields.

UPDATE FormFields
SET
    SectionId = (
        SELECT fs.Id
        FROM FormSections fs
        INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
        WHERE fs.Title = 'Creator Critical — Effectiveness'
          AND ef.Name LIKE '%YouTube%IQA%'
        LIMIT 1
    ),
    "Order" = (
        SELECT COALESCE(MAX(f2."Order"), -1) + 1 + FormFields."Order"
        FROM FormFields f2
        WHERE f2.SectionId = (
            SELECT fs.Id
            FROM FormSections fs
            INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
            WHERE fs.Title = 'Creator Critical — Effectiveness'
              AND ef.Name LIKE '%YouTube%IQA%'
            LIMIT 1
        )
    )
WHERE SectionId = (
    SELECT fs.Id
    FROM FormSections fs
    INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
    WHERE fs.Title = 'Creator Critical — Effort'
      AND ef.Name LIKE '%YouTube%IQA%'
    LIMIT 1
);

-- Reassign all FormFields that belong to "Creator Critical — Engagement"
-- to the "Creator Critical — Effectiveness" section, appending after Effort fields.

UPDATE FormFields
SET
    SectionId = (
        SELECT fs.Id
        FROM FormSections fs
        INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
        WHERE fs.Title = 'Creator Critical — Effectiveness'
          AND ef.Name LIKE '%YouTube%IQA%'
        LIMIT 1
    ),
    "Order" = (
        SELECT COALESCE(MAX(f2."Order"), -1) + 1 + FormFields."Order"
        FROM FormFields f2
        WHERE f2.SectionId = (
            SELECT fs.Id
            FROM FormSections fs
            INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
            WHERE fs.Title = 'Creator Critical — Effectiveness'
              AND ef.Name LIKE '%YouTube%IQA%'
            LIMIT 1
        )
    )
WHERE SectionId = (
    SELECT fs.Id
    FROM FormSections fs
    INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
    WHERE fs.Title = 'Creator Critical — Engagement'
      AND ef.Name LIKE '%YouTube%IQA%'
    LIMIT 1
);

-- ── STEP 3: Rename the surviving section ──────────────────────────────────────

UPDATE FormSections
SET
    Title = 'Creator Critical',
    Description = 'Have we helped the creator with their goal/issue? How much effort did it take, and how did we make them feel?'
WHERE Title = 'Creator Critical — Effectiveness'
  AND FormId = (
      SELECT Id FROM EvaluationForms
      WHERE Name LIKE '%YouTube%IQA%'
      LIMIT 1
  );

-- ── STEP 4: Delete the now-empty source sections ──────────────────────────────

DELETE FROM FormSections
WHERE Title IN ('Creator Critical — Effort', 'Creator Critical — Engagement')
  AND FormId = (
      SELECT Id FROM EvaluationForms
      WHERE Name LIKE '%YouTube%IQA%'
      LIMIT 1
  );

-- ── STEP 5: Verify ────────────────────────────────────────────────────────────
-- Run these SELECT statements to confirm the merge succeeded.

SELECT fs.Title AS SectionTitle, COUNT(ff.Id) AS FieldCount
FROM FormSections fs
LEFT JOIN FormFields ff ON ff.SectionId = fs.Id
INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
WHERE ef.Name LIKE '%YouTube%IQA%'
GROUP BY fs.Id, fs.Title
ORDER BY fs."Order";

-- Expected: One row for "Creator Critical" with 11 fields (3 Effectiveness + 5 Effort + 3 Engagement)
-- Plus "Business Critical" (2 fields) and "Compliance Critical" (3 fields).
