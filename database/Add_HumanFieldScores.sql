-- ============================================================================
-- Migration: Add HumanFieldScores table for per-parameter human review scores
-- Supports: Human-in-the-loop parameter-wise scoring and AI vs Human comparison
-- Run once against any existing QAAutomation database.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'HumanFieldScores'
)
BEGIN
    CREATE TABLE [dbo].[HumanFieldScores] (
        [Id]                INT             IDENTITY(1,1) NOT NULL,
        [HumanReviewItemId] INT             NOT NULL,
        [FieldId]           INT             NOT NULL,
        [AiScore]           FLOAT           NOT NULL DEFAULT 0,
        [HumanScore]        FLOAT           NOT NULL DEFAULT 0,
        [Comment]           NVARCHAR(1000)  NULL,
        CONSTRAINT [PK_HumanFieldScores]
            PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_HumanFieldScores_HumanReviewItems]
            FOREIGN KEY ([HumanReviewItemId])
            REFERENCES [dbo].[HumanReviewItems] ([Id])
            ON DELETE CASCADE,
        CONSTRAINT [FK_HumanFieldScores_FormFields]
            FOREIGN KEY ([FieldId])
            REFERENCES [dbo].[FormFields] ([Id])
            ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX [IX_HumanFieldScores_HumanReviewItemId]
        ON [dbo].[HumanFieldScores] ([HumanReviewItemId]);

    CREATE NONCLUSTERED INDEX [IX_HumanFieldScores_FieldId]
        ON [dbo].[HumanFieldScores] ([FieldId]);

    PRINT 'HumanFieldScores table created successfully.';
END
ELSE
BEGIN
    PRINT 'HumanFieldScores table already exists — skipping creation.';
END
GO
