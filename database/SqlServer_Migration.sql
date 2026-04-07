-- =============================================================================
-- QAAutomation  –  End-to-End SQL Server Migration Script
-- =============================================================================
-- Purpose  : Create the complete QAAutomation schema on SQL Server (any version
--            from SQL Server 2016 / Azure SQL Database onward) and populate it
--            with the same seed data that the application seeds automatically on
--            a fresh SQLite database.
--
-- Usage    : Execute this script against an empty SQL Server database.
--            The script is idempotent where practical – it uses
--            IF NOT EXISTS / IF OBJECT_ID guards so it can be re-run safely.
--
-- SQLite → SQL Server type mapping used throughout:
--   SQLite INTEGER           → INT  (or BIGINT for ContentSizeBytes)
--   SQLite REAL              → FLOAT
--   SQLite TEXT (nullable)   → NVARCHAR(n) NULL
--   SQLite TEXT NOT NULL     → NVARCHAR(n) NOT NULL
--   SQLite INTEGER (boolean) → BIT NOT NULL DEFAULT 0
--   SQLite TEXT (datetime)   → DATETIME2 NULL  (ISO-8601 strings stored by EF)
--   SQLite TEXT (JSON blobs) → NVARCHAR(MAX) NULL
--
-- Key differences from SQLite:
--   • All IDENTITY columns are INT IDENTITY(1,1) PRIMARY KEY.
--   • Cascade / restrict behaviours are expressed as ON DELETE CASCADE /
--     ON DELETE NO ACTION / ON DELETE SET NULL.
--   • Unique indexes are created with CREATE UNIQUE INDEX.
--   • Boolean columns use BIT (0 = false, 1 = true).
--   • All string literals use N'...' (Unicode-safe).
-- =============================================================================

USE master;
GO

-- If you want to create the database, uncomment and customise the block below:
-- IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'QAAutomation')
-- BEGIN
--     CREATE DATABASE QAAutomation
--         COLLATE Latin1_General_100_CI_AS_SC_UTF8;
-- END
-- GO
-- USE QAAutomation;
-- GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- 1. TABLES
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Projects
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Projects]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Projects] (
        [Id]                   INT            NOT NULL IDENTITY(1,1),
        [Name]                 NVARCHAR(200)  NOT NULL,
        [Description]          NVARCHAR(MAX)  NULL,
        [IsActive]             BIT            NOT NULL DEFAULT 1,
        [PiiProtectionEnabled] BIT            NOT NULL DEFAULT 0,
        [PiiRedactionMode]     NVARCHAR(20)   NOT NULL DEFAULT N'Redact',
        [CreatedAt]            DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id])
    );
END
GO

-- -----------------------------------------------------------------------------
-- Lobs  (Lines of Business)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Lobs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Lobs] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [ProjectId]   INT            NOT NULL,
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [IsActive]    BIT            NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Lobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Lobs_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- AppUsers
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppUsers]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AppUsers] (
        [Id]           INT            NOT NULL IDENTITY(1,1),
        [Username]     NVARCHAR(100)  NOT NULL,
        [PasswordHash] NVARCHAR(MAX)  NOT NULL,
        [Email]        NVARCHAR(MAX)  NULL,
        [Role]         NVARCHAR(50)   NOT NULL DEFAULT N'User',
        [IsActive]     BIT            NOT NULL DEFAULT 1,
        [CreatedAt]    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [UX_AppUsers_Username] ON [dbo].[AppUsers] ([Username]);
END
GO

-- -----------------------------------------------------------------------------
-- UserProjectAccesses
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserProjectAccesses]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[UserProjectAccesses] (
        [Id]        INT       NOT NULL IDENTITY(1,1),
        [UserId]    INT       NOT NULL,
        [ProjectId] INT       NOT NULL,
        [GrantedAt] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_UserProjectAccesses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserProjectAccesses_AppUsers]  FOREIGN KEY ([UserId])
            REFERENCES [dbo].[AppUsers]  ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserProjectAccesses_Projects]  FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects]  ([Id]) ON DELETE CASCADE
    );

    -- Prevent duplicate user↔project pairs
    CREATE UNIQUE INDEX [UX_UserProjectAccesses_UserProject]
        ON [dbo].[UserProjectAccesses] ([UserId], [ProjectId]);
END
GO

-- -----------------------------------------------------------------------------
-- EvaluationForms
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EvaluationForms]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[EvaluationForms] (
        [Id]            INT            NOT NULL IDENTITY(1,1),
        [Name]          NVARCHAR(200)  NOT NULL,
        [Description]   NVARCHAR(MAX)  NULL,
        [CreatedAt]     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [IsActive]      BIT            NOT NULL DEFAULT 1,
        [LobId]         INT            NULL,
        -- 0 = Generic (proportional sum); 1 = SectionAutoFail
        [ScoringMethod] INT            NOT NULL DEFAULT 0,
        CONSTRAINT [PK_EvaluationForms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EvaluationForms_Lobs] FOREIGN KEY ([LobId])
            REFERENCES [dbo].[Lobs] ([Id]) ON DELETE NO ACTION
    );
END
ELSE
BEGIN
    -- Backfill: add ScoringMethod to databases created before this column existed
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'[dbo].[EvaluationForms]') AND name = N'ScoringMethod')
        ALTER TABLE [dbo].[EvaluationForms]
            ADD [ScoringMethod] INT NOT NULL CONSTRAINT [DF_EvalForms_ScoringMethod] DEFAULT (0);
END
GO

-- -----------------------------------------------------------------------------
-- FormSections
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FormSections]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[FormSections] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [Title]       NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [Order]       INT            NOT NULL DEFAULT 0,
        [FormId]      INT            NOT NULL,
        CONSTRAINT [PK_FormSections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FormSections_EvaluationForms] FOREIGN KEY ([FormId])
            REFERENCES [dbo].[EvaluationForms] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- FormFields
-- FieldType enum values: Text=0, TextArea=1, Rating=2, Checkbox=3, Dropdown=4, Number=5
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FormFields]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[FormFields] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [Label]       NVARCHAR(200)  NOT NULL,
        [IsRequired]  BIT            NOT NULL DEFAULT 0,
        [Order]       INT            NOT NULL DEFAULT 0,
        [Options]     NVARCHAR(MAX)  NULL,       -- JSON array for Dropdown fields
        [MaxRating]   INT            NOT NULL DEFAULT 5,
        [Description] NVARCHAR(MAX)  NULL,
        [FieldType]   INT            NOT NULL DEFAULT 0,  -- FieldType enum (0=Text … 5=Number)
        [SectionId]   INT            NOT NULL,
        CONSTRAINT [PK_FormFields] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FormFields_FormSections] FOREIGN KEY ([SectionId])
            REFERENCES [dbo].[FormSections] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- EvaluationResults
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EvaluationResults]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[EvaluationResults] (
        [Id]                  INT            NOT NULL IDENTITY(1,1),
        [FormId]              INT            NOT NULL,
        [EvaluatedBy]         NVARCHAR(200)  NOT NULL,
        [EvaluatedAt]         DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [Notes]               NVARCHAR(MAX)  NULL,
        [AgentName]           NVARCHAR(200)  NULL,
        [CallReference]       NVARCHAR(100)  NULL,
        [CallDate]            DATETIME2      NULL,
        [CallDurationSeconds] INT            NULL,
        [OverallReasoning]    NVARCHAR(MAX)  NULL,
        [SentimentJson]       NVARCHAR(MAX)  NULL,   -- JSON blob
        [FieldReasoningJson]  NVARCHAR(MAX)  NULL,   -- JSON array blob
        CONSTRAINT [PK_EvaluationResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EvaluationResults_EvaluationForms] FOREIGN KEY ([FormId])
            REFERENCES [dbo].[EvaluationForms] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- -----------------------------------------------------------------------------
-- EvaluationScores
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EvaluationScores]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[EvaluationScores] (
        [Id]           INT            NOT NULL IDENTITY(1,1),
        [ResultId]     INT            NOT NULL,
        [FieldId]      INT            NOT NULL,
        [Value]        NVARCHAR(MAX)  NOT NULL DEFAULT N'',   -- raw string value ("4", "1", "0", etc.)
        [NumericValue] FLOAT          NULL,
        CONSTRAINT [PK_EvaluationScores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EvaluationScores_EvaluationResults] FOREIGN KEY ([ResultId])
            REFERENCES [dbo].[EvaluationResults] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_EvaluationScores_FormFields] FOREIGN KEY ([FieldId])
            REFERENCES [dbo].[FormFields] ([Id]) ON DELETE NO ACTION
    );
END
GO

-- -----------------------------------------------------------------------------
-- Parameters
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Parameters]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Parameters] (
        [Id]             INT            NOT NULL IDENTITY(1,1),
        [Name]           NVARCHAR(200)  NOT NULL,
        [Description]    NVARCHAR(MAX)  NULL,
        [Category]       NVARCHAR(100)  NULL,
        [DefaultWeight]  FLOAT          NOT NULL DEFAULT 1.0,
        [IsActive]       BIT            NOT NULL DEFAULT 1,
        [CreatedAt]      DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [EvaluationType] NVARCHAR(50)   NOT NULL DEFAULT N'LLM',
        [ProjectId]      INT            NULL,
        CONSTRAINT [PK_Parameters] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Parameters_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- RatingCriteria
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RatingCriteria]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[RatingCriteria] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [MinScore]    INT            NOT NULL DEFAULT 1,
        [MaxScore]    INT            NOT NULL DEFAULT 5,
        [IsActive]    BIT            NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [ProjectId]   INT            NULL,
        CONSTRAINT [PK_RatingCriteria] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RatingCriteria_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- RatingLevels
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RatingLevels]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[RatingLevels] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [CriteriaId]  INT            NOT NULL,
        [Score]       INT            NOT NULL,
        [Label]       NVARCHAR(100)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [Color]       NVARCHAR(20)   NOT NULL DEFAULT N'#6c757d',
        CONSTRAINT [PK_RatingLevels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RatingLevels_RatingCriteria] FOREIGN KEY ([CriteriaId])
            REFERENCES [dbo].[RatingCriteria] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- ParameterClubs
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ParameterClubs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ParameterClubs] (
        [Id]          INT            NOT NULL IDENTITY(1,1),
        [Name]        NVARCHAR(200)  NOT NULL,
        [Description] NVARCHAR(MAX)  NULL,
        [IsActive]    BIT            NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [ProjectId]   INT            NULL,
        CONSTRAINT [PK_ParameterClubs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParameterClubs_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- ParameterClubItems
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ParameterClubItems]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[ParameterClubItems] (
        [Id]               INT    NOT NULL IDENTITY(1,1),
        [ClubId]           INT    NOT NULL,
        [ParameterId]      INT    NOT NULL,
        [Order]            INT    NOT NULL DEFAULT 0,
        [WeightOverride]   FLOAT  NULL,
        [RatingCriteriaId] INT    NULL,
        CONSTRAINT [PK_ParameterClubItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParameterClubItems_ParameterClubs] FOREIGN KEY ([ClubId])
            REFERENCES [dbo].[ParameterClubs] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ParameterClubItems_Parameters] FOREIGN KEY ([ParameterId])
            REFERENCES [dbo].[Parameters] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ParameterClubItems_RatingCriteria] FOREIGN KEY ([RatingCriteriaId])
            REFERENCES [dbo].[RatingCriteria] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- AiConfigs  (singleton row, Id = 1)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AiConfigs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[AiConfigs] (
        [Id]               INT            NOT NULL DEFAULT 1,   -- always 1 (singleton)
        [LlmProvider]      NVARCHAR(50)   NOT NULL DEFAULT N'Google',
        [LlmEndpoint]      NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [LlmApiKey]        NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [LlmDeployment]    NVARCHAR(200)  NOT NULL DEFAULT N'gemini-1.5-pro',
        [LlmTemperature]   FLOAT          NOT NULL DEFAULT 0.1,
        [SentimentProvider] NVARCHAR(50)  NOT NULL DEFAULT N'Google',
        [LanguageEndpoint] NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [LanguageApiKey]   NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [RagTopK]          INT            NOT NULL DEFAULT 3,
        [SpeechEndpoint]   NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [SpeechApiKey]     NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        -- Google Gemini / Google Cloud Speech-to-Text
        [GoogleApiKey]     NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [GoogleGeminiModel] NVARCHAR(100) NOT NULL DEFAULT N'gemini-1.5-pro',
        [SpeechProvider]   NVARCHAR(20)   NOT NULL DEFAULT N'Google',
        [UpdatedAt]        DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_AiConfigs] PRIMARY KEY ([Id])
    );
END
GO

-- -----------------------------------------------------------------------------
-- KnowledgeSources
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[KnowledgeSources]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[KnowledgeSources] (
        [Id]                    INT            NOT NULL IDENTITY(1,1),
        [Name]                  NVARCHAR(200)  NOT NULL,
        [ConnectorType]         NVARCHAR(50)   NOT NULL DEFAULT N'ManualUpload',
        [Description]           NVARCHAR(MAX)  NULL,
        [BlobConnectionString]  NVARCHAR(MAX)  NULL,
        [BlobContainerName]     NVARCHAR(MAX)  NULL,
        [SftpHost]              NVARCHAR(MAX)  NULL,
        [SftpPort]              INT            NULL,
        [SftpUsername]          NVARCHAR(MAX)  NULL,
        [SftpPassword]          NVARCHAR(MAX)  NULL,
        [SftpPath]              NVARCHAR(MAX)  NULL,
        [SharePointSiteUrl]     NVARCHAR(MAX)  NULL,
        [SharePointClientId]    NVARCHAR(MAX)  NULL,
        [SharePointClientSecret] NVARCHAR(MAX) NULL,
        [SharePointLibraryName] NVARCHAR(MAX)  NULL,
        [IsActive]              BIT            NOT NULL DEFAULT 1,
        [CreatedAt]             DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [LastSyncedAt]          DATETIME2      NULL,
        [ProjectId]             INT            NULL,
        CONSTRAINT [PK_KnowledgeSources] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KnowledgeSources_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- KnowledgeDocuments
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[KnowledgeDocuments]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[KnowledgeDocuments] (
        [Id]               INT            NOT NULL IDENTITY(1,1),
        [SourceId]         INT            NOT NULL,
        [Title]            NVARCHAR(500)  NOT NULL,
        [FileName]         NVARCHAR(MAX)  NULL,
        [Content]          NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [Tags]             NVARCHAR(MAX)  NULL,
        [ContentSizeBytes] BIGINT         NOT NULL DEFAULT 0,
        [UploadedAt]       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_KnowledgeDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KnowledgeDocuments_KnowledgeSources] FOREIGN KEY ([SourceId])
            REFERENCES [dbo].[KnowledgeSources] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- SamplingPolicies
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SamplingPolicies]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[SamplingPolicies] (
        [Id]                 INT            NOT NULL IDENTITY(1,1),
        [Name]               NVARCHAR(200)  NOT NULL,
        [Description]        NVARCHAR(MAX)  NULL,
        [ProjectId]          INT            NULL,
        [CallTypeFilter]     NVARCHAR(MAX)  NULL,
        [MinDurationSeconds] INT            NULL,
        [MaxDurationSeconds] INT            NULL,
        [SamplingMethod]     NVARCHAR(20)   NOT NULL DEFAULT N'Percentage',
        [SampleValue]        FLOAT          NOT NULL DEFAULT 10,
        [IsActive]           BIT            NOT NULL DEFAULT 1,
        [CreatedAt]          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]          NVARCHAR(200)  NOT NULL DEFAULT N'',
        CONSTRAINT [PK_SamplingPolicies] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SamplingPolicies_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- HumanReviewItems
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HumanReviewItems]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[HumanReviewItems] (
        [Id]                  INT            NOT NULL IDENTITY(1,1),
        [EvaluationResultId]  INT            NOT NULL,
        [SamplingPolicyId]    INT            NULL,
        [SampledAt]           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [SampledBy]           NVARCHAR(200)  NOT NULL DEFAULT N'system',
        [AssignedTo]          NVARCHAR(200)  NULL,
        [Status]              NVARCHAR(20)   NOT NULL DEFAULT N'Pending',
        [ReviewerComment]     NVARCHAR(MAX)  NULL,
        [ReviewVerdict]       NVARCHAR(20)   NULL,
        [ReviewedBy]          NVARCHAR(200)  NULL,
        [ReviewedAt]          DATETIME2      NULL,
        CONSTRAINT [PK_HumanReviewItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HumanReviewItems_EvaluationResults] FOREIGN KEY ([EvaluationResultId])
            REFERENCES [dbo].[EvaluationResults] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HumanReviewItems_SamplingPolicies] FOREIGN KEY ([SamplingPolicyId])
            REFERENCES [dbo].[SamplingPolicies] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- HumanFieldScores
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HumanFieldScores]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[HumanFieldScores] (
        [Id]                INT            NOT NULL IDENTITY(1,1),
        [HumanReviewItemId] INT            NOT NULL,
        [FieldId]           INT            NOT NULL,
        [AiScore]           FLOAT          NOT NULL DEFAULT 0,
        [HumanScore]        FLOAT          NOT NULL DEFAULT 0,
        [Comment]           NVARCHAR(1000) NULL,
        CONSTRAINT [PK_HumanFieldScores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_HumanFieldScores_HumanReviewItems] FOREIGN KEY ([HumanReviewItemId])
            REFERENCES [dbo].[HumanReviewItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_HumanFieldScores_FormFields] FOREIGN KEY ([FieldId])
            REFERENCES [dbo].[FormFields] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_HumanFieldScores_HumanReviewItemId]
        ON [dbo].[HumanFieldScores] ([HumanReviewItemId]);

    CREATE NONCLUSTERED INDEX [IX_HumanFieldScores_FieldId]
        ON [dbo].[HumanFieldScores] ([FieldId]);
END
GO

-- -----------------------------------------------------------------------------
-- TrainingPlans
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TrainingPlans]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[TrainingPlans] (
        [Id]                  INT            NOT NULL IDENTITY(1,1),
        [Title]               NVARCHAR(300)  NOT NULL,
        [Description]         NVARCHAR(MAX)  NULL,
        [AgentName]           NVARCHAR(200)  NOT NULL DEFAULT N'',
        [AgentUsername]       NVARCHAR(200)  NULL,
        [TrainerName]         NVARCHAR(200)  NOT NULL DEFAULT N'',
        [TrainerUsername]     NVARCHAR(200)  NULL,
        [Status]              NVARCHAR(20)   NOT NULL DEFAULT N'Draft',
        [DueDate]             DATETIME2      NULL,
        [ProjectId]           INT            NULL,
        [EvaluationResultId]  INT            NULL,
        [HumanReviewItemId]   INT            NULL,
        [CreatedBy]           NVARCHAR(200)  NOT NULL DEFAULT N'',
        [CreatedAt]           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [ClosedBy]            NVARCHAR(200)  NULL,
        [ClosedAt]            DATETIME2      NULL,
        [ClosingNotes]        NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_TrainingPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainingPlans_Projects]          FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects]          ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TrainingPlans_EvaluationResults] FOREIGN KEY ([EvaluationResultId])
            REFERENCES [dbo].[EvaluationResults] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TrainingPlans_HumanReviewItems]  FOREIGN KEY ([HumanReviewItemId])
            REFERENCES [dbo].[HumanReviewItems]  ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- TrainingPlanItems
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TrainingPlanItems]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[TrainingPlanItems] (
        [Id]              INT            NOT NULL IDENTITY(1,1),
        [TrainingPlanId]  INT            NOT NULL,
        [TargetArea]      NVARCHAR(200)  NOT NULL DEFAULT N'',
        [ItemType]        NVARCHAR(30)   NOT NULL DEFAULT N'Observation',
        [Content]         NVARCHAR(MAX)  NOT NULL DEFAULT N'',
        [Status]          NVARCHAR(20)   NOT NULL DEFAULT N'Pending',
        [Order]           INT            NOT NULL DEFAULT 0,
        [CompletedBy]     NVARCHAR(200)  NULL,
        [CompletedAt]     DATETIME2      NULL,
        [CompletionNotes] NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_TrainingPlanItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TrainingPlanItems_TrainingPlans] FOREIGN KEY ([TrainingPlanId])
            REFERENCES [dbo].[TrainingPlans] ([Id]) ON DELETE CASCADE
    );
END
GO

-- -----------------------------------------------------------------------------
-- CallPipelineJobs
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CallPipelineJobs]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CallPipelineJobs] (
        [Id]                      INT            NOT NULL IDENTITY(1,1),
        [Name]                    NVARCHAR(200)  NOT NULL,
        [SourceType]              NVARCHAR(50)   NOT NULL DEFAULT N'BatchUrl',
        [FormId]                  INT            NOT NULL,
        [ProjectId]               INT            NULL,
        [Status]                  NVARCHAR(20)   NOT NULL DEFAULT N'Pending',
        [CreatedAt]               DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [StartedAt]               DATETIME2      NULL,
        [CompletedAt]             DATETIME2      NULL,
        [CreatedBy]               NVARCHAR(200)  NOT NULL DEFAULT N'',
        [SftpHost]                NVARCHAR(MAX)  NULL,
        [SftpPort]                INT            NULL,
        [SftpUsername]            NVARCHAR(MAX)  NULL,
        [SftpPassword]            NVARCHAR(MAX)  NULL,
        [SftpPath]                NVARCHAR(MAX)  NULL,
        [SharePointSiteUrl]       NVARCHAR(MAX)  NULL,
        [SharePointClientId]      NVARCHAR(MAX)  NULL,
        [SharePointClientSecret]  NVARCHAR(MAX)  NULL,
        [SharePointLibraryName]   NVARCHAR(MAX)  NULL,
        [RecordingPlatformUrl]    NVARCHAR(MAX)  NULL,
        [RecordingPlatformApiKey] NVARCHAR(MAX)  NULL,
        [RecordingPlatformTenantId] NVARCHAR(MAX) NULL,
        [FilterFromDate]          NVARCHAR(MAX)  NULL,
        [FilterToDate]            NVARCHAR(MAX)  NULL,
        [ErrorMessage]            NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_CallPipelineJobs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallPipelineJobs_EvaluationForms] FOREIGN KEY ([FormId])
            REFERENCES [dbo].[EvaluationForms] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CallPipelineJobs_Projects] FOREIGN KEY ([ProjectId])
            REFERENCES [dbo].[Projects] ([Id]) ON DELETE SET NULL
    );
END
GO

-- -----------------------------------------------------------------------------
-- CallPipelineItems
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CallPipelineItems]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CallPipelineItems] (
        [Id]                  INT            NOT NULL IDENTITY(1,1),
        [JobId]               INT            NOT NULL,
        [SourceReference]     NVARCHAR(MAX)  NULL,
        [AgentName]           NVARCHAR(MAX)  NULL,
        [CallReference]       NVARCHAR(MAX)  NULL,
        [CallDate]            DATETIME2      NULL,
        [Status]              NVARCHAR(20)   NOT NULL DEFAULT N'Pending',
        [CreatedAt]           DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [ProcessedAt]         DATETIME2      NULL,
        [ErrorMessage]        NVARCHAR(MAX)  NULL,
        [EvaluationResultId]  INT            NULL,
        [ScorePercent]        FLOAT          NULL,
        [AiReasoning]         NVARCHAR(MAX)  NULL,
        CONSTRAINT [PK_CallPipelineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallPipelineItems_CallPipelineJobs] FOREIGN KEY ([JobId])
            REFERENCES [dbo].[CallPipelineJobs] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CallPipelineItems_EvaluationResults] FOREIGN KEY ([EvaluationResultId])
            REFERENCES [dbo].[EvaluationResults] ([Id]) ON DELETE SET NULL
    );
END
GO


-- =============================================================================
-- 2. SEED DATA
-- =============================================================================
-- All seed blocks are wrapped in IF NOT EXISTS guards so the script can be
-- re-run without duplicating data.
-- =============================================================================

-- ─────────────────────────────────────────────────────────────────────────────
-- 2.1  Admin user
-- Password hash = SHA-256("Admin@123") rendered as lowercase hex
-- SHA-256("Admin@123") = a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[AppUsers])
BEGIN
    SET IDENTITY_INSERT [dbo].[AppUsers] ON;
    INSERT INTO [dbo].[AppUsers] ([Id],[Username],[PasswordHash],[Email],[Role],[IsActive],[CreatedAt])
    VALUES (1, N'admin',
            N'a665a45920422f9d417e4867efdc4fb8a04a1f3fff1fa07e998e86f7f7a27ae3',
            N'admin@qaautomation.local', N'Admin', 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[AppUsers] OFF;
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2.2  Default AI Config  (singleton row)
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[AiConfigs])
BEGIN
    INSERT INTO [dbo].[AiConfigs] (
        [Id],[LlmProvider],[LlmEndpoint],[LlmApiKey],[LlmDeployment],[LlmTemperature],
        [SentimentProvider],[LanguageEndpoint],[LanguageApiKey],[RagTopK],
        [SpeechEndpoint],[SpeechApiKey],[GoogleApiKey],[GoogleGeminiModel],[SpeechProvider],[UpdatedAt])
    VALUES (
        1, N'Google', N'', N'', N'gemini-1.5-pro', 0.1,
        N'Google', N'', N'', 3,
        N'', N'', N'', N'gemini-1.5-pro', N'Google', SYSUTCDATETIME());
END
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2.3  Capital One Project + LOB
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[Projects] WHERE [Name] = N'Capital One')
BEGIN
    SET IDENTITY_INSERT [dbo].[Projects] ON;
    INSERT INTO [dbo].[Projects] ([Id],[Name],[Description],[IsActive],[PiiProtectionEnabled],[PiiRedactionMode],[CreatedAt])
    VALUES (1, N'Capital One', N'Capital One Financial Corporation', 1, 0, N'Redact', SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[Projects] OFF;

    SET IDENTITY_INSERT [dbo].[Lobs] ON;
    INSERT INTO [dbo].[Lobs] ([Id],[ProjectId],[Name],[Description],[IsActive],[CreatedAt])
    VALUES (1, 1, N'Customer Support Call',
            N'Customer support call centre quality evaluation', 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[Lobs] OFF;

    -- Grant admin access to project
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UserProjectAccesses] WHERE [UserId]=1 AND [ProjectId]=1)
    BEGIN
        INSERT INTO [dbo].[UserProjectAccesses] ([UserId],[ProjectId],[GrantedAt])
        VALUES (1, 1, SYSUTCDATETIME());
    END

    -- ── Rating Criteria ──────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[RatingCriteria] ON;
    INSERT INTO [dbo].[RatingCriteria] ([Id],[Name],[Description],[MinScore],[MaxScore],[IsActive],[CreatedAt],[ProjectId])
    VALUES
        (1, N'QA Score (1-5)',
            N'Standard quality score from 1 (Unacceptable) to 5 (Outstanding)',
            1, 5, 1, SYSUTCDATETIME(), 1),
        (2, N'Compliance (Pass/Fail)',
            N'Binary compliance check - failure is auto-fail',
            0, 1, 1, SYSUTCDATETIME(), 1);
    SET IDENTITY_INSERT [dbo].[RatingCriteria] OFF;

    INSERT INTO [dbo].[RatingLevels] ([CriteriaId],[Score],[Label],[Description],[Color])
    VALUES
        (1, 1, N'Unacceptable',       N'Critical failure affecting customer or compliance',    N'#dc3545'),
        (1, 2, N'Needs Improvement',  N'Below standard performance',                           N'#fd7e14'),
        (1, 3, N'Meets Standard',     N'Satisfactory performance meeting expectations',         N'#ffc107'),
        (1, 4, N'Exceeds Standard',   N'Above average performance',                            N'#20c997'),
        (1, 5, N'Outstanding',        N'Exemplary performance, exceeded all expectations',     N'#198754'),
        (2, 0, N'FAIL',               N'Non-compliant — requires immediate coaching',          N'#dc3545'),
        (2, 1, N'PASS',               N'Compliant with policy and regulation',                 N'#198754');

    -- ── Parameters ────────────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[Parameters] ON;
    INSERT INTO [dbo].[Parameters] ([Id],[Name],[Description],[Category],[DefaultWeight],[IsActive],[CreatedAt],[EvaluationType],[ProjectId])
    VALUES
        -- Call Opening
        ( 1, N'Professional Greeting',
              N'Agent uses approved greeting script with brand name, own name, and offer to help',
              N'Call Opening', 1.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 2, N'Customer Identity Verification',
              N'Completes full CID verification per PCI/security policy before discussing account',
              N'Call Opening', 2.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 3, N'Brand Introduction',
              N'Sets the right tone and properly introduces Capital One services',
              N'Call Opening', 1.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        -- Issue Resolution
        ( 4, N'First Call Resolution',
              N'Resolves customer issue completely without need for callback or transfer',
              N'Issue Resolution', 3.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 5, N'Product & Policy Knowledge',
              N'Demonstrates accurate knowledge of Capital One credit card products, rates, and policies',
              N'Issue Resolution', 2.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 6, N'Information Accuracy',
              N'All information provided to customer is accurate and up to date',
              N'Issue Resolution', 2.5, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 7, N'Problem-Solving Ability',
              N'Effectively identifies root cause and provides appropriate resolution or alternatives',
              N'Issue Resolution', 2.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        -- Communication Skills
        ( 8, N'Verbal Clarity & Articulation',
              N'Speaks clearly, avoids jargon, adjusts language to customer''s level',
              N'Communication Skills', 1.5, 1, SYSUTCDATETIME(), N'LLM', 1),
        ( 9, N'Active Listening',
              N'Demonstrates understanding, does not interrupt, confirms understanding before proceeding',
              N'Communication Skills', 1.5, 1, SYSUTCDATETIME(), N'LLM', 1),
        (10, N'Empathy & Rapport Building',
              N'Acknowledges customer emotions, personalizes the interaction, builds trust',
              N'Communication Skills', 2.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        (11, N'Pace, Tone & Energy',
              N'Maintains professional tone throughout, appropriate pace, positive energy',
              N'Communication Skills', 1.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        -- Compliance & Procedures
        (12, N'CFPB Regulatory Compliance',
              N'Adheres to all CFPB regulations including fair lending, UDAAP, and debt collection rules',
              N'Compliance & Procedures', 5.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        (13, N'Required Disclosures',
              N'Provides all mandatory disclosures (APR, fees, payment terms) as required by Reg Z',
              N'Compliance & Procedures', 3.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        (14, N'PCI Data Security',
              N'Does not capture, repeat, or store sensitive payment card data in violation of PCI DSS',
              N'Compliance & Procedures', 5.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        -- Call Closing
        (15, N'Issue Summary & Confirmation',
              N'Summarizes resolution and confirms customer satisfaction before closing',
              N'Call Closing', 1.5, 1, SYSUTCDATETIME(), N'LLM', 1),
        (16, N'Offer of Further Assistance',
              N'Proactively asks if customer needs anything else before ending the call',
              N'Call Closing', 1.0, 1, SYSUTCDATETIME(), N'LLM', 1),
        (17, N'Professional Sign-Off',
              N'Uses approved closing script, thanks customer, and ends call professionally',
              N'Call Closing', 1.0, 1, SYSUTCDATETIME(), N'LLM', 1);
    SET IDENTITY_INSERT [dbo].[Parameters] OFF;

    -- ── Parameter Clubs ───────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[ParameterClubs] ON;
    INSERT INTO [dbo].[ParameterClubs] ([Id],[Name],[Description],[IsActive],[CreatedAt],[ProjectId])
    VALUES
        (1, N'Call Opening',
            N'Initial call handling — greeting, verification, and brand introduction',
            1, SYSUTCDATETIME(), 1),
        (2, N'Issue Resolution',
            N'Effectiveness in understanding and resolving the customer''s credit card issue',
            1, SYSUTCDATETIME(), 1),
        (3, N'Communication Skills',
            N'Quality of verbal communication and relationship building',
            1, SYSUTCDATETIME(), 1),
        (4, N'Compliance & Procedures',
            N'Adherence to regulatory and internal compliance requirements — violations are auto-fail',
            1, SYSUTCDATETIME(), 1),
        (5, N'Call Closing',
            N'Professional and thorough call conclusion',
            1, SYSUTCDATETIME(), 1);
    SET IDENTITY_INSERT [dbo].[ParameterClubs] OFF;

    INSERT INTO [dbo].[ParameterClubItems] ([ClubId],[ParameterId],[RatingCriteriaId],[Order])
    VALUES
        -- Call Opening
        (1,  1, 1, 0), (1,  2, 2, 1), (1,  3, 1, 2),
        -- Issue Resolution
        (2,  4, 1, 0), (2,  5, 1, 1), (2,  6, 1, 2), (2,  7, 1, 3),
        -- Communication Skills
        (3,  8, 1, 0), (3,  9, 1, 1), (3, 10, 1, 2), (3, 11, 1, 3),
        -- Compliance & Procedures
        (4, 12, 2, 0), (4, 13, 2, 1), (4, 14, 2, 2),
        -- Call Closing
        (5, 15, 1, 0), (5, 16, 1, 1), (5, 17, 1, 2);

    -- ── Evaluation Form ────────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[EvaluationForms] ON;
    INSERT INTO [dbo].[EvaluationForms] ([Id],[Name],[Description],[IsActive],[LobId],[CreatedAt],[UpdatedAt])
    VALUES (1,
        N'Capital One — Credit Card Customer Support QA Form',
        N'Quality evaluation form for Capital One credit card customer support interactions. Covers call handling, issue resolution, communication, compliance, and closing.',
        1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[EvaluationForms] OFF;

    -- Sections
    SET IDENTITY_INSERT [dbo].[FormSections] ON;
    INSERT INTO [dbo].[FormSections] ([Id],[Title],[Description],[Order],[FormId])
    VALUES
        (1, N'Call Opening',            NULL, 0, 1),
        (2, N'Issue Resolution',        NULL, 1, 1),
        (3, N'Communication Skills',    NULL, 2, 1),
        (4, N'Compliance & Procedures', NULL, 3, 1),
        (5, N'Call Closing',            NULL, 4, 1);
    SET IDENTITY_INSERT [dbo].[FormSections] OFF;

    -- Fields  (FieldType 2 = Rating)
    SET IDENTITY_INSERT [dbo].[FormFields] ON;
    INSERT INTO [dbo].[FormFields] ([Id],[Label],[IsRequired],[Order],[MaxRating],[FieldType],[SectionId])
    VALUES
        -- Call Opening (SectionId=1)
        ( 1, N'Professional Greeting',           1, 0, 5, 2, 1),
        ( 2, N'Customer Identity Verification',  1, 1, 1, 2, 1),
        ( 3, N'Brand Introduction',              0, 2, 5, 2, 1),
        -- Issue Resolution (SectionId=2)
        ( 4, N'First Call Resolution',           1, 0, 5, 2, 2),
        ( 5, N'Product & Policy Knowledge',      0, 1, 5, 2, 2),
        ( 6, N'Information Accuracy',            1, 2, 5, 2, 2),
        ( 7, N'Problem-Solving Ability',         0, 3, 5, 2, 2),
        -- Communication Skills (SectionId=3)
        ( 8, N'Verbal Clarity & Articulation',   0, 0, 5, 2, 3),
        ( 9, N'Active Listening',                0, 1, 5, 2, 3),
        (10, N'Empathy & Rapport Building',      0, 2, 5, 2, 3),
        (11, N'Pace, Tone & Energy',             0, 3, 5, 2, 3),
        -- Compliance & Procedures (SectionId=4)
        (12, N'CFPB Regulatory Compliance',      1, 0, 1, 2, 4),
        (13, N'Required Disclosures',            1, 1, 1, 2, 4),
        (14, N'PCI Data Security',               1, 2, 1, 2, 4),
        -- Call Closing (SectionId=5)
        (15, N'Issue Summary & Confirmation',    0, 0, 5, 2, 5),
        (16, N'Offer of Further Assistance',     0, 1, 5, 2, 5),
        (17, N'Professional Sign-Off',           0, 2, 5, 2, 5);
    SET IDENTITY_INSERT [dbo].[FormFields] OFF;

    -- ── Evaluation Results (5 sample audits) ─────────────────────────────────
    SET IDENTITY_INSERT [dbo].[EvaluationResults] ON;
    INSERT INTO [dbo].[EvaluationResults]
        ([Id],[FormId],[EvaluatedBy],[EvaluatedAt],[Notes],[AgentName],[CallReference],[CallDate])
    VALUES
        (1, 1, N'admin', '2025-01-15 09:00:00',
            N'Excellent across all areas. Strong compliance adherence and outstanding customer rapport.',
            N'Sarah Mitchell',  N'COF-2025-00142', '2025-01-14'),
        (2, 1, N'admin', '2025-01-22 10:00:00',
            N'Good performance overall. Missed required APR disclosure on the first attempt — corrected after customer prompt.',
            N'James Kowalski',  N'COF-2025-00215', '2025-01-21'),
        (3, 1, N'admin', '2025-02-05 11:00:00',
            N'Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout.',
            N'Priya Nair',      N'COF-2025-00318', '2025-02-04'),
        (4, 1, N'admin', '2025-02-12 12:00:00',
            N'Below standard. Failed to complete full CID verification and asked customer to repeat card number aloud — PCI violation. Immediate coaching required.',
            N'Derek Thompson',  N'COF-2025-00401', '2025-02-11'),
        (5, 1, N'admin', '2025-02-20 13:00:00',
            N'Solid performance with good first-call resolution. Slight hesitation on product knowledge for balance transfer promotions.',
            N'Maria Gonzalez',  N'COF-2025-00487', '2025-02-19');
    SET IDENTITY_INSERT [dbo].[EvaluationResults] OFF;

    -- ── Evaluation Scores ─────────────────────────────────────────────────────
    -- Sarah Mitchell (ResultId=1)
    INSERT INTO [dbo].[EvaluationScores] ([ResultId],[FieldId],[Value],[NumericValue]) VALUES
        (1,  1, N'4', 4),(1,  2, N'1', 1),(1,  3, N'4', 4),
        (1,  4, N'5', 5),(1,  5, N'4', 4),(1,  6, N'5', 5),(1,  7, N'4', 4),
        (1,  8, N'5', 5),(1,  9, N'4', 4),(1, 10, N'5', 5),(1, 11, N'4', 4),
        (1, 12, N'1', 1),(1, 13, N'1', 1),(1, 14, N'1', 1),
        (1, 15, N'5', 5),(1, 16, N'5', 5),(1, 17, N'4', 4);
    -- James Kowalski (ResultId=2) – Required Disclosures FAIL
    INSERT INTO [dbo].[EvaluationScores] ([ResultId],[FieldId],[Value],[NumericValue]) VALUES
        (2,  1, N'4', 4),(2,  2, N'1', 1),(2,  3, N'3', 3),
        (2,  4, N'4', 4),(2,  5, N'3', 3),(2,  6, N'4', 4),(2,  7, N'3', 3),
        (2,  8, N'4', 4),(2,  9, N'3', 3),(2, 10, N'3', 3),(2, 11, N'3', 3),
        (2, 12, N'1', 1),(2, 13, N'0', 0),(2, 14, N'1', 1),
        (2, 15, N'3', 3),(2, 16, N'3', 3),(2, 17, N'4', 4);
    -- Priya Nair (ResultId=3) – All 5s
    INSERT INTO [dbo].[EvaluationScores] ([ResultId],[FieldId],[Value],[NumericValue]) VALUES
        (3,  1, N'5', 5),(3,  2, N'1', 1),(3,  3, N'5', 5),
        (3,  4, N'5', 5),(3,  5, N'5', 5),(3,  6, N'5', 5),(3,  7, N'5', 5),
        (3,  8, N'5', 5),(3,  9, N'5', 5),(3, 10, N'5', 5),(3, 11, N'5', 5),
        (3, 12, N'1', 1),(3, 13, N'1', 1),(3, 14, N'1', 1),
        (3, 15, N'5', 5),(3, 16, N'5', 5),(3, 17, N'5', 5);
    -- Derek Thompson (ResultId=4) – CID + PCI FAIL
    INSERT INTO [dbo].[EvaluationScores] ([ResultId],[FieldId],[Value],[NumericValue]) VALUES
        (4,  1, N'3', 3),(4,  2, N'0', 0),(4,  3, N'2', 2),
        (4,  4, N'2', 2),(4,  5, N'2', 2),(4,  6, N'3', 3),(4,  7, N'2', 2),
        (4,  8, N'3', 3),(4,  9, N'2', 2),(4, 10, N'2', 2),(4, 11, N'2', 2),
        (4, 12, N'1', 1),(4, 13, N'1', 1),(4, 14, N'0', 0),
        (4, 15, N'2', 2),(4, 16, N'2', 2),(4, 17, N'3', 3);
    -- Maria Gonzalez (ResultId=5)
    INSERT INTO [dbo].[EvaluationScores] ([ResultId],[FieldId],[Value],[NumericValue]) VALUES
        (5,  1, N'5', 5),(5,  2, N'1', 1),(5,  3, N'4', 4),
        (5,  4, N'4', 4),(5,  5, N'3', 3),(5,  6, N'4', 4),(5,  7, N'4', 4),
        (5,  8, N'4', 4),(5,  9, N'4', 4),(5, 10, N'4', 4),(5, 11, N'4', 4),
        (5, 12, N'1', 1),(5, 13, N'1', 1),(5, 14, N'1', 1),
        (5, 15, N'4', 4),(5, 16, N'4', 4),(5, 17, N'5', 5);

    -- ── Sampling Policies ─────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[SamplingPolicies] ON;
    INSERT INTO [dbo].[SamplingPolicies]
        ([Id],[Name],[Description],[ProjectId],[CallTypeFilter],[SamplingMethod],[SampleValue],[IsActive],[CreatedBy],[CreatedAt],[UpdatedAt])
    VALUES
        (1, N'10% Random Sampling',
            N'Sample 10% of all completed evaluations for human review',
            1, NULL, N'Percentage', 10, 1, N'admin', SYSUTCDATETIME(), SYSUTCDATETIME()),
        (2, N'Compliance Risk — All Failing Calls',
            N'100% review for calls with compliance-related failures',
            1, N'Compliance', N'Percentage', 100, 1, N'admin', SYSUTCDATETIME(), SYSUTCDATETIME()),
        (3, N'New Agents — 5 Calls per Week',
            N'Review the first 5 calls per week for newly onboarded agents',
            1, NULL, N'Count', 5, 1, N'admin', SYSUTCDATETIME(), SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[SamplingPolicies] OFF;

    -- ── Human Review Items ────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[HumanReviewItems] ON;
    INSERT INTO [dbo].[HumanReviewItems]
        ([Id],[EvaluationResultId],[SamplingPolicyId],[SampledAt],[SampledBy],[AssignedTo],
         [Status],[ReviewerComment],[ReviewVerdict],[ReviewedBy],[ReviewedAt])
    VALUES
        (1, 1, 1, '2025-01-15 10:00:00', N'system', N'admin',
            N'Reviewed',
            N'Agree with the AI scoring. Agent demonstrated excellent product knowledge and compliance.',
            N'Agree', N'admin', '2025-01-16 09:00:00'),
        (2, 2, 1, '2025-01-22 11:00:00', N'system', N'admin',
            N'Reviewed',
            N'Partially agree — the Required Disclosures failure was borderline; agent did mention APR but not in the required format.',
            N'Partial', N'admin', '2025-01-23 09:00:00'),
        (3, 4, 1, '2025-02-12 14:00:00', N'system', N'admin',
            N'Pending', NULL, NULL, NULL, NULL);
    SET IDENTITY_INSERT [dbo].[HumanReviewItems] OFF;

    -- ── Training Plans ────────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[TrainingPlans] ON;
    INSERT INTO [dbo].[TrainingPlans]
        ([Id],[Title],[Description],[AgentName],[TrainerName],[Status],[DueDate],
         [ProjectId],[EvaluationResultId],[HumanReviewItemId],[CreatedBy],[CreatedAt],[UpdatedAt])
    VALUES
        (1,
            N'PCI DSS Compliance Remediation — Derek Thompson',
            N'Immediate coaching required following PCI DSS violation (COF-2025-00401). Agent asked customer to repeat card number verbally and failed full CID verification.',
            N'Derek Thompson', N'admin', N'Active', '2025-03-10',
            1, 4, NULL, N'admin', '2025-02-12', '2025-02-12'),
        (2,
            N'Required Disclosures Coaching — James Kowalski',
            N'APR and fee disclosure was not provided in the required format on call COF-2025-00215. Coaching to reinforce Reg Z disclosure requirements.',
            N'James Kowalski', N'admin', N'InProgress', '2025-03-05',
            1, 2, NULL, N'admin', '2025-01-22', '2025-02-01');
    SET IDENTITY_INSERT [dbo].[TrainingPlans] OFF;

    SET IDENTITY_INSERT [dbo].[TrainingPlanItems] ON;
    INSERT INTO [dbo].[TrainingPlanItems]
        ([Id],[TrainingPlanId],[TargetArea],[ItemType],[Content],[Status],[Order],[CompletedBy],[CompletedAt],[CompletionNotes])
    VALUES
        -- Plan 1 – Derek Thompson
        (1, 1, N'Compliance & Procedures', N'Observation',
            N'Agent asked the customer to repeat their full card number aloud, violating PCI DSS data security requirements.',
            N'Pending', 0, NULL, NULL, NULL),
        (2, 1, N'Compliance & Procedures', N'Observation',
            N'Customer Identity Verification (CID) process was not completed before account details were discussed.',
            N'Pending', 1, NULL, NULL, NULL),
        (3, 1, N'Compliance & Procedures', N'Recommendation',
            N'Complete the PCI DSS e-learning module (30 min) and pass the assessment with a minimum score of 80%.',
            N'Pending', 2, NULL, NULL, NULL),
        (4, 1, N'Compliance & Procedures', N'Recommendation',
            N'Complete a live role-play session with the trainer covering CID verification and sensitive data handling.',
            N'Pending', 3, NULL, NULL, NULL),
        (5, 1, N'Call Opening', N'Recommendation',
            N'Review the CID verification checklist and practise the verification script until it becomes second nature.',
            N'Pending', 4, NULL, NULL, NULL),
        -- Plan 2 – James Kowalski
        (6, 2, N'Compliance & Procedures', N'Observation',
            N'Required APR and fee disclosures were mentioned but not delivered in the mandatory scripted format as required by Reg Z.',
            N'Done', 0, N'admin', '2025-02-01', N'Agent reviewed the Reg Z disclosure script and confirmed understanding.'),
        (7, 2, N'Compliance & Procedures', N'Recommendation',
            N'Review the Reg Z required disclosure scripts for all Capital One credit card product categories.',
            N'InProgress', 1, NULL, NULL, NULL),
        (8, 2, N'Compliance & Procedures', N'Recommendation',
            N'Conduct two supervised calls where the trainer monitors disclosure delivery in real time.',
            N'Pending', 2, NULL, NULL, NULL);
    SET IDENTITY_INSERT [dbo].[TrainingPlanItems] OFF;

    -- ── Call Pipeline Jobs ────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[CallPipelineJobs] ON;
    INSERT INTO [dbo].[CallPipelineJobs]
        ([Id],[Name],[SourceType],[FormId],[ProjectId],[Status],[CreatedAt],[StartedAt],[CompletedAt],[CreatedBy])
    VALUES
        (1, N'Capital One — Weekly Batch Jan W3 2025', N'BatchUrl', 1, 1,
            N'Completed', '2025-01-20', '2025-01-20 09:00:00', '2025-01-20 09:18:00', N'admin'),
        (2, N'Capital One — Weekly Batch Feb W1 2025', N'BatchUrl', 1, 1,
            N'Completed', '2025-02-03', '2025-02-03 09:00:00', '2025-02-03 09:22:00', N'admin');
    SET IDENTITY_INSERT [dbo].[CallPipelineJobs] OFF;

    SET IDENTITY_INSERT [dbo].[CallPipelineItems] ON;
    INSERT INTO [dbo].[CallPipelineItems]
        ([Id],[JobId],[SourceReference],[AgentName],[CallReference],[CallDate],[Status],[CreatedAt],[ProcessedAt],[ErrorMessage],[EvaluationResultId],[ScorePercent],[AiReasoning])
    VALUES
        -- Job 1
        (1, 1,
            N'https://recordings.capitalone.internal/calls/COF-2025-00142.mp3',
            N'Sarah Mitchell',  N'COF-2025-00142', '2025-01-14',
            N'Completed', '2025-01-20', '2025-01-20 09:05:00', NULL, 1, 91.2,
            N'Excellent performance across all evaluation areas. Strong compliance adherence and outstanding customer rapport.'),
        (2, 1,
            N'https://recordings.capitalone.internal/calls/COF-2025-00215.mp3',
            N'James Kowalski',  N'COF-2025-00215', '2025-01-21',
            N'Completed', '2025-01-20', '2025-01-20 09:10:00', NULL, 2, 73.5,
            N'Good overall performance. Missed required APR disclosure on the first attempt — corrected after customer prompt.'),
        (3, 1,
            N'https://recordings.capitalone.internal/calls/COF-2025-00155.mp3',
            N'Maria Gonzalez',  N'COF-2025-00155', '2025-01-16',
            N'Failed', '2025-01-20', '2025-01-20 09:15:00',
            N'Audio quality too poor to transcribe reliably (SNR < 10 dB).',
            NULL, NULL, NULL),
        -- Job 2
        (4, 2,
            N'https://recordings.capitalone.internal/calls/COF-2025-00318.mp3',
            N'Priya Nair',      N'COF-2025-00318', '2025-02-04',
            N'Completed', '2025-02-03', '2025-02-03 09:08:00', NULL, 3, 98.5,
            N'Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout.'),
        (5, 2,
            N'https://recordings.capitalone.internal/calls/COF-2025-00401.mp3',
            N'Derek Thompson',  N'COF-2025-00401', '2025-02-11',
            N'Completed', '2025-02-03', '2025-02-03 09:16:00', NULL, 4, 38.2,
            N'Below standard. Failed CID verification and PCI DSS violation detected. Immediate coaching required.');
    SET IDENTITY_INSERT [dbo].[CallPipelineItems] OFF;

    -- ── Knowledge Base ────────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[KnowledgeSources] ON;
    INSERT INTO [dbo].[KnowledgeSources]
        ([Id],[Name],[ConnectorType],[Description],[IsActive],[CreatedAt],[LastSyncedAt],[ProjectId])
    VALUES (1,
        N'Capital One QA Policy Documents',
        N'ManualUpload',
        N'Internal QA policies, compliance guidelines, and evaluation rubrics for Capital One customer support',
        1, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
    SET IDENTITY_INSERT [dbo].[KnowledgeSources] OFF;

    SET IDENTITY_INSERT [dbo].[KnowledgeDocuments] ON;
    INSERT INTO [dbo].[KnowledgeDocuments] ([Id],[SourceId],[Title],[FileName],[Content],[Tags],[ContentSizeBytes],[UploadedAt])
    VALUES
        (1, 1,
            N'PCI DSS Agent Guidelines v4.0',
            N'PCI_DSS_Agent_Guidelines_v4.pdf',
            N'PCI DSS Scope for Call Centre Agents

Agents MUST NOT:
- Ask customers to read aloud full card numbers
- Write down, store, or repeat Primary Account Numbers (PAN) in any form
- Record sensitive authentication data after authorisation

Agents MUST:
- Complete CID verification before accessing any account information
- Use masked card numbers (last 4 digits only) when confirming identity
- Immediately terminate calls where customers attempt to provide full card numbers verbally and request they use the secure IVR channel

Violations are classified as Critical and result in immediate remediation and mandatory retraining.',
            N'PCI,Compliance,Security,CID', 1240, '2025-01-01'),
        (2, 1,
            N'Reg Z Required Disclosures Script',
            N'RegZ_Disclosure_Script_2025.pdf',
            N'Regulation Z — Required Verbal Disclosures for Credit Card Calls

When discussing APR or fees, agents must deliver the following scripted disclosure:

"Just to let you know, [Product Name] has a variable APR of [X]% for purchases, [Y]% for cash advances, and a minimum interest charge of $[Z]. Late fees are up to $[amount]. Please refer to your Cardmember Agreement for full terms."

This disclosure is mandatory whenever:
- Opening a new account
- Discussing promotional rates
- Responding to balance transfer enquiries
- Any conversation involving credit terms or fees

Failure to deliver this disclosure in full is a Reg Z compliance violation.',
            N'Compliance,RegZ,Disclosures,Fees', 980, '2025-01-01'),
        (3, 1,
            N'QA Evaluation Rubric — Communication Skills',
            N'QA_Rubric_Communication_2025.pdf',
            N'Communication Skills Evaluation Rubric

Score 5 (Outstanding): Agent communicates with exceptional clarity, warmth, and professionalism. Uses the customer''s name naturally, matches their communication style, and uses precise, jargon-free language throughout.

Score 4 (Exceeds Standard): Clear and professional communication with minor inconsistencies. Customer-centric language used. Active listening demonstrated.

Score 3 (Meets Standard): Acceptable communication. Occasional lapses in clarity or empathy but no significant impact on customer experience.

Score 2 (Needs Improvement): Unclear communication, interrupts customer, or uses inappropriate tone. Customer experience negatively impacted.

Score 1 (Unacceptable): Rude, dismissive, or seriously unclear communication. Immediate coaching required.',
            N'Communication,Rubric,Scoring,Training', 870, '2025-01-15');
    SET IDENTITY_INSERT [dbo].[KnowledgeDocuments] OFF;

END -- END Capital One project guard
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2.4  YouTube Project
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM [dbo].[Projects] WHERE [Name] = N'Youtube')
BEGIN
    SET IDENTITY_INSERT [dbo].[Projects] ON;
    INSERT INTO [dbo].[Projects] ([Id],[Name],[Description],[IsActive],[PiiProtectionEnabled],[PiiRedactionMode],[CreatedAt])
    VALUES (2, N'Youtube',
            N'YouTube Creator Support Operations — Internal Quality Assurance',
            1, 0, N'Redact', SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[Projects] OFF;

    SET IDENTITY_INSERT [dbo].[Lobs] ON;
    INSERT INTO [dbo].[Lobs] ([Id],[ProjectId],[Name],[Description],[IsActive],[CreatedAt])
    VALUES (2, 2, N'CSO',
            N'Creator Support Operations line of business', 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[Lobs] OFF;

    -- Grant admin access
    IF NOT EXISTS (SELECT 1 FROM [dbo].[UserProjectAccesses] WHERE [UserId]=1 AND [ProjectId]=2)
    BEGIN
        INSERT INTO [dbo].[UserProjectAccesses] ([UserId],[ProjectId],[GrantedAt])
        VALUES (1, 2, SYSUTCDATETIME());
    END

    -- ── Rating Criteria (Pass/Fail) ────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[RatingCriteria] ON;
    INSERT INTO [dbo].[RatingCriteria] ([Id],[Name],[Description],[MinScore],[MaxScore],[IsActive],[CreatedAt],[ProjectId])
    VALUES (3,
        N'YouTube IQA — Pass/Fail',
        N'Non-compensatory pass/fail used across all YouTube IQA competencies. Failure in any mandatory competency results in auto-fail for that category.',
        0, 1, 1, SYSUTCDATETIME(), 2);
    SET IDENTITY_INSERT [dbo].[RatingCriteria] OFF;

    INSERT INTO [dbo].[RatingLevels] ([CriteriaId],[Score],[Label],[Description],[Color])
    VALUES
        (3, 0, N'FAIL', N'Competency not met — triggers category auto-fail', N'#dc3545'),
        (3, 1, N'PASS', N'Competency met or not applicable (Yes/NA)',          N'#198754');

    -- ── Parameters (16 YouTube IQA competencies) ──────────────────────────────
    SET IDENTITY_INSERT [dbo].[Parameters] ON;
    INSERT INTO [dbo].[Parameters] ([Id],[Name],[Description],[Category],[DefaultWeight],[IsActive],[CreatedAt],[EvaluationType],[ProjectId])
    VALUES
        -- Creator Critical — Effectiveness
        (18, N'Accuracy',
              N'Did the creator receive an accurate and complete solution for all the informed issues?',
              N'Creator Critical — Effectiveness', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (19, N'Tailoring',
              N'Were the issues or expectations of the creator met with the right level of personalisation?',
              N'Creator Critical — Effectiveness', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (20, N'Obviation & Next Steps',
              N'Has the creator been equipped with relevant obviation opportunities and next steps?',
              N'Creator Critical — Effectiveness', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        -- Creator Critical — Effort
        (21, N'Responsiveness',
              N'Have we set and/or kept expectations with regards to timely and proactive follow-up communications?',
              N'Creator Critical — Effort', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (22, N'Internal Coordination',
              N'Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?',
              N'Creator Critical — Effort', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (23, N'Workflows Adherence',
              N'Did we minimise creator effort by following correct workflows?',
              N'Creator Critical — Effort', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (24, N'Creator Feedback',
              N'Was the creator reassured that their feedback was captured and addressed?',
              N'Creator Critical — Effort', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (25, N'CSAT Survey',
              N'Was the creator appropriately asked to provide feedback through a CSAT survey?',
              N'Creator Critical — Effort', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        -- Creator Critical — Engagement
        (26, N'Clarity',
              N'Has the creator received clear communication through the use of correct language and effective questioning?',
              N'Creator Critical — Engagement', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (27, N'Empathy',
              N'Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?',
              N'Creator Critical — Engagement', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (28, N'Tone',
              N'Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?',
              N'Creator Critical — Engagement', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        -- Business Critical
        (29, N'Due Diligence',
              N'Did the agent complete all required due-diligence steps before responding or escalating?',
              N'Business Critical', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (30, N'Issue Tagging',
              N'Was the case correctly tagged / categorised using Neo Categorization?',
              N'Business Critical', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        -- Compliance Critical
        (31, N'Authentication',
              N'Did the agent follow the correct authentication process before discussing account or creator details?',
              N'Compliance Critical', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (32, N'Keep YouTube Safe',
              N'Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?',
              N'Compliance Critical', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2),
        (33, N'Policy',
              N'Did the agent correctly apply and communicate YouTube policies relevant to the creator''s issue?',
              N'Compliance Critical', 1.0, 1, SYSUTCDATETIME(), N'LLM', 2);
    SET IDENTITY_INSERT [dbo].[Parameters] OFF;

    -- ── Parameter Clubs ───────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[ParameterClubs] ON;
    INSERT INTO [dbo].[ParameterClubs] ([Id],[Name],[Description],[IsActive],[CreatedAt],[ProjectId])
    VALUES
        (6, N'Creator Critical – Effectiveness',
            N'Measures whether the creator received an accurate, tailored, and complete solution including relevant next steps.',
            1, SYSUTCDATETIME(), 2),
        (7, N'Creator Critical – Effort',
            N'Measures how much effort was required for the creator to reach resolution, covering responsiveness, coordination, workflows, and feedback loops.',
            1, SYSUTCDATETIME(), 2),
        (8, N'Creator Critical – Engagement',
            N'Measures how the creator felt during the interaction in terms of communication clarity, empathy, and professional tone.',
            1, SYSUTCDATETIME(), 2),
        (9, N'Business Critical',
            N'Non-compensatory business-critical checks: due diligence and correct issue tagging.',
            1, SYSUTCDATETIME(), 2),
        (10, N'Compliance Critical',
            N'Non-compensatory compliance checks: authentication, trust & safety, and policy adherence.',
            1, SYSUTCDATETIME(), 2);
    SET IDENTITY_INSERT [dbo].[ParameterClubs] OFF;

    INSERT INTO [dbo].[ParameterClubItems] ([ClubId],[ParameterId],[RatingCriteriaId],[Order])
    VALUES
        -- Effectiveness
        (6, 18, 3, 0), (6, 19, 3, 1), (6, 20, 3, 2),
        -- Effort
        (7, 21, 3, 0), (7, 22, 3, 1), (7, 23, 3, 2), (7, 24, 3, 3), (7, 25, 3, 4),
        -- Engagement
        (8, 26, 3, 0), (8, 27, 3, 1), (8, 28, 3, 2),
        -- Business Critical
        (9, 29, 3, 0), (9, 30, 3, 1),
        -- Compliance Critical
        (10, 31, 3, 0), (10, 32, 3, 1), (10, 33, 3, 2);

    -- ── Evaluation Form ────────────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[EvaluationForms] ON;
    INSERT INTO [dbo].[EvaluationForms] ([Id],[Name],[Description],[IsActive],[LobId],[CreatedAt],[UpdatedAt])
    VALUES (2,
        N'YouTube CSO IQA Evaluation Form',
        N'Internal Quality Assurance evaluation form for YouTube Creator Support Operations. Based on the YouTube CSO QA Framework covering Effectiveness, Effort, Engagement, Business Critical, and Compliance Critical competencies.',
        1, 2, SYSUTCDATETIME(), SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[EvaluationForms] OFF;

    SET IDENTITY_INSERT [dbo].[FormSections] ON;
    INSERT INTO [dbo].[FormSections] ([Id],[Title],[Description],[Order],[FormId])
    VALUES
        (6,  N'Creator Critical – Effectiveness',
             N'Have we helped the creator with their goal/issue?', 0, 2),
        (7,  N'Creator Critical – Effort',
             N'How much effort was it for the creator to get a resolution?', 1, 2),
        (8,  N'Creator Critical – Engagement',
             N'How did we make the creator feel during their interaction?', 2, 2),
        (9,  N'Business Critical',
             N'Non-compensatory business-critical competencies — failure in any one triggers an auto-fail for this category.', 3, 2),
        (10, N'Compliance Critical',
             N'Non-compensatory compliance competencies — failure in any one triggers an auto-fail for this category.', 4, 2);
    SET IDENTITY_INSERT [dbo].[FormSections] OFF;

    SET IDENTITY_INSERT [dbo].[FormFields] ON;
    INSERT INTO [dbo].[FormFields] ([Id],[Label],[Description],[IsRequired],[Order],[MaxRating],[FieldType],[SectionId])
    VALUES
        -- Effectiveness (SectionId=6)
        (18, N'Accuracy',
             N'Did the creator receive an accurate and complete solution for all the informed issues?',
             1, 0, 1, 2, 6),
        (19, N'Tailoring',
             N'Were the issues or expectations of the creator met with the right level of personalisation?',
             1, 1, 1, 2, 6),
        (20, N'Obviation & Next Steps',
             N'Has the creator been equipped with relevant obviation opportunities and next steps?',
             1, 2, 1, 2, 6),
        -- Effort (SectionId=7)
        (21, N'Responsiveness',
             N'Have we set and/or kept expectations with regards to timely and proactive follow-up communications?',
             1, 0, 1, 2, 7),
        (22, N'Internal Coordination',
             N'Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?',
             1, 1, 1, 2, 7),
        (23, N'Workflows Adherence',
             N'Did we minimise creator effort by following correct workflows?',
             1, 2, 1, 2, 7),
        (24, N'Creator Feedback',
             N'Was the creator reassured that their feedback was captured and addressed?',
             1, 3, 1, 2, 7),
        (25, N'CSAT Survey',
             N'Was the creator appropriately asked to provide feedback through a CSAT survey?',
             0, 4, 1, 2, 7),
        -- Engagement (SectionId=8)
        (26, N'Clarity',
             N'Has the creator received clear communication through the use of correct language and effective questioning?',
             1, 0, 1, 2, 8),
        (27, N'Empathy',
             N'Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?',
             1, 1, 1, 2, 8),
        (28, N'Tone',
             N'Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?',
             1, 2, 1, 2, 8),
        -- Business Critical (SectionId=9)
        (29, N'Due Diligence',
             N'Did the agent complete all required due-diligence steps before responding or escalating?',
             1, 0, 1, 2, 9),
        (30, N'Issue Tagging',
             N'Was the case correctly tagged / categorised using Neo Categorization?',
             1, 1, 1, 2, 9),
        -- Compliance Critical (SectionId=10)
        (31, N'Authentication',
             N'Did the agent follow the correct authentication process before discussing account or creator details?',
             1, 0, 1, 2, 10),
        (32, N'Keep YouTube Safe',
             N'Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?',
             1, 1, 1, 2, 10),
        (33, N'Policy',
             N'Did the agent correctly apply and communicate YouTube policies relevant to the creator''s issue?',
             1, 2, 1, 2, 10);
    SET IDENTITY_INSERT [dbo].[FormFields] OFF;

    -- ── YouTube Knowledge Base ─────────────────────────────────────────────────
    SET IDENTITY_INSERT [dbo].[KnowledgeSources] ON;
    INSERT INTO [dbo].[KnowledgeSources]
        ([Id],[Name],[ConnectorType],[Description],[IsActive],[CreatedAt],[LastSyncedAt],[ProjectId])
    VALUES (2,
        N'YouTube IQA Assessment Guidelines',
        N'ManualUpload',
        N'YouTube Creator Support Operations IQA framework — competency definitions, assessment guidelines, and scoring criteria used for quality evaluation.',
        1, SYSUTCDATETIME(), SYSUTCDATETIME(), 2);
    SET IDENTITY_INSERT [dbo].[KnowledgeSources] OFF;

    SET IDENTITY_INSERT [dbo].[KnowledgeDocuments] ON;
    INSERT INTO [dbo].[KnowledgeDocuments]
        ([Id],[SourceId],[Title],[FileName],[Content],[Tags],[ContentSizeBytes],[UploadedAt])
    VALUES (4, 2,
        N'YouTube CSO IQA — Assessment Guidelines',
        N'YouTube_CSO_IQA_Assessment_Guidelines.pdf',
        N'YouTube Creator Support Operations — IQA Assessment Guidelines

OVERVIEW
All competencies use a non-compensatory Pass / Fail scale. PASS = competency fully met or not applicable (Yes/NA). FAIL triggers an auto-fail for the relevant category.

CREATOR CRITICAL — EFFECTIVENESS
1. ACCURACY: Did the creator receive an accurate and complete solution for all the informed issues?
   PASS: Correct, complete answer for every issue raised. No misinformation or omissions.
   FAIL: Inaccurate information, partial resolution, or no usable resolution.

2. TAILORING: Were the issues or expectations of the creator met with the right level of personalisation?
   PASS: Agent adapted response to creator''s specific context. No generic copy-paste where personalisation was possible.
   FAIL: Templated/generic response in a situation requiring personalised support.

3. OBVIATION & NEXT STEPS: Has the creator been equipped with relevant obviation opportunities and next steps?
   PASS: Proactively offered self-service resources, preventive steps, or clear next actions.
   FAIL: Interaction closed without equipping creator with next steps or resources.

CREATOR CRITICAL — EFFORT
4. RESPONSIVENESS: Have we set and/or kept expectations with regards to timely and proactive follow-up communications?
   PASS: Issue acknowledged promptly, clear timelines set, proactive follow-up where delays occurred.
   FAIL: Creator had to chase for updates, or timelines were not set or missed without communication.

5. INTERNAL COORDINATION: Did we reduce creator effort by effectively connecting them with the right internal teams?
   PASS: Escalation or consultation facilitated seamlessly. Creator not asked to re-explain or contact other teams.
   FAIL: Creator bounced between teams, asked to re-explain, or agent failed to engage the right resource.

6. WORKFLOWS ADHERENCE: Did we minimise creator effort by following correct workflows?
   PASS: All prescribed workflows (Neo case management, escalation paths) followed correctly.
   FAIL: Deviations from required workflows led to delays, rework, or additional creator effort.

7. CREATOR FEEDBACK: Was the creator reassured that their feedback was captured and addressed?
   PASS: Creator feedback acknowledged, confirmed it would be logged, appropriate expectations set.
   FAIL: Feedback dismissed, ignored, or no acknowledgement given.

8. CSAT SURVEY: Was the creator appropriately asked to provide feedback through a CSAT survey?
   PASS: CSAT invitation delivered in accordance with guidelines (correct timing, correct channel, no coaching language).
   FAIL: Survey omitted, wrong timing, or agent used language that could influence the creator''s rating.

CREATOR CRITICAL — ENGAGEMENT
9. CLARITY: Has the creator received clear communication through the use of correct language and effective questioning?
   PASS: All communication clear, concise, jargon-free. Effective questioning to confirm understanding.
   FAIL: Unclear communication, unexplained technical terms, or failure to confirm understanding.

10. EMPATHY: Was the creator reassured that there was a clear understanding of the goal or problem?
    PASS: Agent acknowledged creator''s situation, demonstrated understanding of urgency and emotional weight.
    FAIL: Agent dismissive, failed to acknowledge frustration or urgency.

11. TONE: Did the creator receive consistently professional and respectful communications?
    PASS: All communications professional, warm, consistent with YouTube Tone & Voice guidelines.
    FAIL: Tone inappropriate, inconsistent, or not aligned with YouTube brand guidelines.

BUSINESS CRITICAL
12. DUE DILIGENCE: Did the agent complete all required due-diligence steps before responding or escalating?
    PASS: All mandatory checks completed (account verification, case history review, policy lookup).
    FAIL: Agent responded or escalated without completing required checks.

13. ISSUE TAGGING: Was the case correctly tagged / categorised using Neo Categorization?
    PASS: Case tagged with correct primary and secondary categories.
    FAIL: Case tagged incorrectly, incompletely, or not tagged at all.

COMPLIANCE CRITICAL
14. AUTHENTICATION: Did the agent follow the correct authentication process?
    PASS: Prescribed authentication workflow followed in full before accessing account information. N/A → PASS.
    FAIL: Account information disclosed or account actions taken without completing authentication.

15. KEEP YOUTUBE SAFE: Did the agent adhere to all policies that keep YouTube and its creators safe?
    PASS: Full compliance with Trust & Safety and Content Policy obligations.
    FAIL: Failed to escalate T&S concern, shared endangering information, or breached safety obligations.

16. POLICY: Did the agent correctly apply and communicate YouTube policies?
    PASS: Correct YouTube policy applied and communicated clearly and accurately.
    FAIL: Wrong policy applied, policy communicated inaccurately, or relevant policy omitted.

SCORING SUMMARY
Total competencies: 16 | Scale: Pass/Fail (non-compensatory within each category)
Auto-fail categories: Business Critical (Due Diligence OR Issue Tagging fail) | Compliance Critical (any competency fail)
Overall score = (PASS count / total assessed) × 100%. Business Critical and Compliance Critical expect 100%.',
        N'YouTube,IQA,Guidelines,Assessment,Quality', 7200, SYSUTCDATETIME());
    SET IDENTITY_INSERT [dbo].[KnowledgeDocuments] OFF;

END -- END YouTube project guard
GO


-- =============================================================================
-- 3. APPLICATION CONFIGURATION CHANGES FOR SQL SERVER
-- =============================================================================
-- To switch the application from SQLite to SQL Server:
--
-- 3a. Install the SQL Server EF Core provider in QAAutomation.API.csproj:
--       <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.*" />
--
-- 3b. In Program.cs, replace:
--       options.UseSqlite(connStr)
--     with:
--       options.UseSqlServer(connStr)
--
-- 3c. Update appsettings.json (or environment variable CONNECTIONSTRINGS__DEFAULTCONNECTION):
--       "ConnectionStrings": {
--         "DefaultConnection": "Server=your-server;Database=QAAutomation;Trusted_Connection=True;TrustServerCertificate=True;"
--       }
--
-- 3d. The inline ALTER TABLE column-migration block in Program.cs (which uses
--     SQLite PRAGMA) must be replaced with SQL Server equivalents, e.g.:
--       IF NOT EXISTS (SELECT 1 FROM sys.columns
--                      WHERE object_id = OBJECT_ID('TableName') AND name = 'ColumnName')
--           ALTER TABLE TableName ADD ColumnName NVARCHAR(MAX) NOT NULL DEFAULT '';
--
-- The EF Core model configuration in OnModelCreating is provider-agnostic and
-- requires no changes for SQL Server compatibility.
-- =============================================================================

-- =============================================================================
-- 4. COLUMN SIZE FIXES (run against existing databases)
-- =============================================================================
-- CallPipelineItems.SourceReference was originally NVARCHAR(2000).
-- File-upload jobs store the full data-URI of the uploaded transcript in this
-- column, which can far exceed 2000 characters.  Widen it to NVARCHAR(MAX).
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CallPipelineItems')
      AND name      = N'SourceReference'
      AND max_length <> -1   -- -1 means MAX in sys.columns
)
BEGIN
    ALTER TABLE [dbo].[CallPipelineItems]
        ALTER COLUMN [SourceReference] NVARCHAR(MAX) NULL;
    PRINT N'Widened CallPipelineItems.SourceReference to NVARCHAR(MAX).';
END
GO

PRINT N'Migration completed successfully.';
GO
