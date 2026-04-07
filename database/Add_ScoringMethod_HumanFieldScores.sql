-- =============================================================================
-- Migration: Add ScoringMethod column to EvaluationForms
--          + Add HumanFieldScores table (if not present)
-- =============================================================================
-- Run ONCE on any existing QAAutomation SQL Server database.
-- Both blocks are guarded so re-running is safe (idempotent).
--
-- Why this is needed
-- ------------------
-- 1. ScoringMethod  — the EF model gained a ScoringMethod property that
--    controls whether scoring uses Generic proportional sums (0) or
--    SectionAutoFail (1) where a zero on any field in a section zeroes the
--    whole section.  Databases created from an older SQL script won't have
--    this column and the application will fail to read EvaluationForms.
--
-- 2. HumanFieldScores — the SqlServer_Migration.sql script did not include
--    the HumanFieldScores table (it was added separately as
--    Add_HumanFieldScores.sql).  Databases set up from the migration script
--    are missing this table, which causes the HITL verdict submission to fail
--    silently (SaveChangesAsync throws) so human scores are never persisted.
-- =============================================================================

SET NOCOUNT ON;
GO

-- =============================================================================
-- PART 1 — ScoringMethod column on EvaluationForms
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.EvaluationForms')
      AND  name      = N'ScoringMethod'
)
BEGIN
    ALTER TABLE dbo.EvaluationForms
        ADD ScoringMethod INT NOT NULL
            CONSTRAINT DF_EvalForms_ScoringMethod DEFAULT (0);

    -- ScoringMethod values:
    --   0 = Generic     (proportional sum, used by Capital One and most forms)
    --   1 = SectionAutoFail (a zero on any field zeroes the whole section,
    --                        used by YouTube IQA)
    -- The DEFAULT (0) means all existing forms silently remain Generic, which
    -- is the correct backward-compatible behaviour.

    -- Upgrade the YouTube IQA form to SectionAutoFail (mirrors what the
    -- application's EF seed does on every startup).
    UPDATE ef
    SET    ef.ScoringMethod = 1
    FROM   dbo.EvaluationForms ef
    JOIN   dbo.Lobs            l  ON l.Id = ef.LobId
    WHERE  l.Name = 'CSO'
      AND  ef.Name LIKE '%YouTube%';

    PRINT 'ScoringMethod column added to EvaluationForms.';
END
ELSE
BEGIN
    PRINT 'ScoringMethod column already exists on EvaluationForms — skipping.';
END
GO

-- =============================================================================
-- PART 2 — HumanFieldScores table
-- =============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM   sys.objects
    WHERE  object_id = OBJECT_ID(N'dbo.HumanFieldScores')
      AND  type      = N'U'
)
BEGIN
    CREATE TABLE dbo.HumanFieldScores
    (
        Id                  INT            IDENTITY(1,1) NOT NULL,
        HumanReviewItemId   INT            NOT NULL,
        FieldId             INT            NOT NULL,
        AiScore             FLOAT          NOT NULL CONSTRAINT DF_HFS_AiScore    DEFAULT (0),
        HumanScore          FLOAT          NOT NULL CONSTRAINT DF_HFS_HumanScore DEFAULT (0),
        Comment             NVARCHAR(1000) NULL,

        CONSTRAINT PK_HumanFieldScores    PRIMARY KEY (Id),
        CONSTRAINT FK_HFS_HumanReviewItem FOREIGN KEY (HumanReviewItemId)
            REFERENCES dbo.HumanReviewItems (Id) ON DELETE CASCADE,
        CONSTRAINT FK_HFS_FormField       FOREIGN KEY (FieldId)
            REFERENCES dbo.FormFields (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_HumanFieldScores_HumanReviewItemId
        ON dbo.HumanFieldScores (HumanReviewItemId);

    CREATE NONCLUSTERED INDEX IX_HumanFieldScores_FieldId
        ON dbo.HumanFieldScores (FieldId);

    PRINT 'HumanFieldScores table created successfully.';
END
ELSE
BEGIN
    PRINT 'HumanFieldScores table already exists — skipping.';
END
GO

-- =============================================================================
-- Verification
-- =============================================================================

-- Confirm ScoringMethod column exists
SELECT
    c.name            AS ColumnName,
    tp.name           AS DataType,
    c.is_nullable     AS IsNullable,
    dc.definition     AS DefaultValue
FROM   sys.columns         c
JOIN   sys.types           tp ON tp.user_type_id = c.user_type_id
LEFT   JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
WHERE  c.object_id = OBJECT_ID(N'dbo.EvaluationForms')
  AND  c.name      = N'ScoringMethod';
-- Expected: ColumnName=ScoringMethod, DataType=int, IsNullable=0, DefaultValue=((0))

-- Confirm HumanFieldScores table exists with correct shape
SELECT
    c.name        AS ColumnName,
    tp.name       AS DataType
FROM   sys.columns c
JOIN   sys.types   tp ON tp.user_type_id = c.user_type_id
WHERE  c.object_id = OBJECT_ID(N'dbo.HumanFieldScores')
ORDER  BY c.column_id;
-- Expected columns: Id, HumanReviewItemId, FieldId, AiScore, HumanScore, Comment

-- Confirm ScoringMethod values are set correctly on existing forms
SELECT
    ef.Name,
    ef.ScoringMethod,
    CASE ef.ScoringMethod
        WHEN 0 THEN 'Generic'
        WHEN 1 THEN 'SectionAutoFail'
        ELSE 'Unknown'
    END AS ScoringMethodName
FROM   dbo.EvaluationForms ef
ORDER  BY ef.Id;
GO
