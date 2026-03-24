-- =============================================================================
-- Script: Merge YouTube "Creator Critical" sections & clubs into one
-- =============================================================================
-- Targets the YouTube CSO IQA tenant and merges:
--
--   FormSections  (EvaluationForm):
--     "Creator Critical — Effectiveness"  ─┐
--     "Creator Critical — Effort"          ├─► "Creator Critical"
--     "Creator Critical — Engagement"     ─┘
--
--   ParameterClubs:
--     "Creator Critical – Effectiveness"  ─┐
--     "Creator Critical – Effort"          ├─► "Creator Critical"
--     "Creator Critical – Engagement"     ─┘
--
-- ⚠️  DATABASE COMPATIBILITY ⚠️
--   Written for SQLite (the default database used by this project).
--   For SQL Server replace every  LIMIT 1  inside a sub-select with
--   the equivalent  SELECT TOP 1 ...  syntax.
--
-- ⚠️  DASH VARIANTS ⚠️
--   The original seed may have used either:
--     em dash (—  U+2014)  e.g. "Creator Critical — Effectiveness"
--     en dash (–  U+2013)  e.g. "Creator Critical – Effectiveness"
--   LIKE '%Creator Critical%Effectiveness%' matches both automatically.
--
-- ⚠️  FIX NOTES (vs previous version) ⚠️
--   • Fixed: SQLite does not allow a correlated reference to the table being
--     updated (FormFields."Order") inside a scalar sub-select in the SET
--     clause. The old MAX()+1+FormFields."Order" pattern caused an error.
--     Solution: use fixed offsets (+100 / +200) instead — no sub-select needed.
--   • Added: ParameterClubs / ParameterClubItems merge (Part 2) so that the
--     three separate clubs are consolidated into one "Creator Critical" club.
--
-- Run ONCE on any database that still has the 3 split sections/clubs.
-- Fresh installs from the current seed are already merged; this script will
-- silently do nothing on them (WHERE clauses match 0 rows).
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- PART 1 — EvaluationForm: FormSections & FormFields
-- ─────────────────────────────────────────────────────────────────────────────
-- Keep the "Effectiveness" section as the survivor (lowest Order).
-- Reassign Effort fields (+100 offset) and Engagement fields (+200 offset)
-- into it, then rename and delete the empty donor sections.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1a. Move FormFields: Effort section → Effectiveness section
UPDATE FormFields
SET
    SectionId = (
        SELECT fs.Id
        FROM FormSections fs
        INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
        WHERE fs.Title LIKE '%Creator Critical%Effectiveness%'
          AND ef.Name LIKE '%YouTube%'
        LIMIT 1
    ),
    "Order" = "Order" + 100
WHERE SectionId IN (
    SELECT fs.Id
    FROM FormSections fs
    INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
    WHERE fs.Title LIKE '%Creator Critical%Effort%'
      AND ef.Name LIKE '%YouTube%'
);

-- 1b. Move FormFields: Engagement section → Effectiveness section
UPDATE FormFields
SET
    SectionId = (
        SELECT fs.Id
        FROM FormSections fs
        INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
        WHERE fs.Title LIKE '%Creator Critical%Effectiveness%'
          AND ef.Name LIKE '%YouTube%'
        LIMIT 1
    ),
    "Order" = "Order" + 200
WHERE SectionId IN (
    SELECT fs.Id
    FROM FormSections fs
    INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
    WHERE fs.Title LIKE '%Creator Critical%Engagement%'
      AND ef.Name LIKE '%YouTube%'
);

-- 1c. Rename the surviving section
UPDATE FormSections
SET
    Title       = 'Creator Critical',
    Description = 'Have we helped the creator with their goal/issue? How much effort did it take, and how did we make them feel?'
WHERE Title LIKE '%Creator Critical%Effectiveness%'
  AND FormId IN (
      SELECT Id FROM EvaluationForms WHERE Name LIKE '%YouTube%'
  );

-- 1d. Delete the now-empty donor sections
DELETE FROM FormSections
WHERE Title LIKE '%Creator Critical%Effort%'
   OR Title LIKE '%Creator Critical%Engagement%';

-- ─────────────────────────────────────────────────────────────────────────────
-- PART 2 — ParameterClubs & ParameterClubItems
-- ─────────────────────────────────────────────────────────────────────────────
-- Mirror Part 1 for the ParameterClub side so that the three separate clubs
-- are consolidated into a single "Creator Critical" club containing all 11
-- parameter items (3 Effectiveness + 5 Effort + 3 Engagement).
-- ─────────────────────────────────────────────────────────────────────────────

-- 2a. Move ParameterClubItems: Effort club → Effectiveness club
UPDATE ParameterClubItems
SET
    ClubId  = (
        SELECT Id FROM ParameterClubs
        WHERE Name LIKE '%Creator Critical%Effectiveness%'
          AND ProjectId = (
              SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
          )
        LIMIT 1
    ),
    "Order" = "Order" + 100
WHERE ClubId IN (
    SELECT Id FROM ParameterClubs
    WHERE Name LIKE '%Creator Critical%Effort%'
      AND ProjectId = (
          SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
      )
);

-- 2b. Move ParameterClubItems: Engagement club → Effectiveness club
UPDATE ParameterClubItems
SET
    ClubId  = (
        SELECT Id FROM ParameterClubs
        WHERE Name LIKE '%Creator Critical%Effectiveness%'
          AND ProjectId = (
              SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
          )
        LIMIT 1
    ),
    "Order" = "Order" + 200
WHERE ClubId IN (
    SELECT Id FROM ParameterClubs
    WHERE Name LIKE '%Creator Critical%Engagement%'
      AND ProjectId = (
          SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
      )
);

-- 2c. Rename the surviving ParameterClub
UPDATE ParameterClubs
SET
    Name        = 'Creator Critical',
    Description = 'Measures whether the creator received the right solution with the right effort and the right communication style.'
WHERE Name LIKE '%Creator Critical%Effectiveness%'
  AND ProjectId = (
      SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
  );

-- 2d. Delete the now-empty donor clubs
DELETE FROM ParameterClubs
WHERE (Name LIKE '%Creator Critical%Effort%' OR Name LIKE '%Creator Critical%Engagement%')
  AND ProjectId = (
      SELECT Id FROM Projects WHERE Name LIKE '%YouTube%' LIMIT 1
  );

-- ─────────────────────────────────────────────────────────────────────────────
-- PART 3 — Verification
-- Run these SELECTs after the script to confirm the merge succeeded.
-- ─────────────────────────────────────────────────────────────────────────────

-- 3a. Form sections and field counts
SELECT
    fs.Title      AS SectionTitle,
    fs."Order"    AS SectionOrder,
    COUNT(ff.Id)  AS FieldCount
FROM FormSections fs
LEFT JOIN FormFields ff ON ff.SectionId = fs.Id
INNER JOIN EvaluationForms ef ON ef.Id = fs.FormId
WHERE ef.Name LIKE '%YouTube%'
GROUP BY fs.Id, fs.Title, fs."Order"
ORDER BY fs."Order";
-- Expected:
--   "Creator Critical"    11 fields  (3 Effectiveness + 5 Effort + 3 Engagement)
--   "Business Critical"    2 fields
--   "Compliance Critical"  3 fields

-- 3b. Parameter clubs and item counts
SELECT
    pc.Name       AS ClubName,
    COUNT(pci.Id) AS ItemCount
FROM ParameterClubs pc
LEFT JOIN ParameterClubItems pci ON pci.ClubId = pc.Id
INNER JOIN Projects p ON p.Id = pc.ProjectId
WHERE p.Name LIKE '%YouTube%'
GROUP BY pc.Id, pc.Name
ORDER BY pc.Name;
-- Expected:
--   "Business Critical"    2 items
--   "Compliance Critical"  3 items
--   "Creator Critical"    11 items  (3 + 5 + 3)
