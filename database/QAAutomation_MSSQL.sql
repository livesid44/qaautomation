-- =============================================================================
-- QA Automation Platform — Microsoft SQL Server Setup Script
-- =============================================================================
-- Generates the complete schema and representative seed data.
--
-- Usage:
--   sqlcmd -S <server> -U <user> -P <password> -i QAAutomation_MSSQL.sql
--   OR open in SSMS and execute against an empty (or new) database.
--
-- Idempotent: drops all objects first so the script can be re-run safely.
-- Order:      1. Database (optional — comment out if targeting an existing DB)
--             2. Tables   (dependency order, leaves first)
--             3. Indexes & Constraints
--             4. Seed Data
-- =============================================================================

USE master;
GO

-- ── 1. Create database (skip / comment out if DB already exists) ──────────────
IF DB_ID(N'QAAutomation') IS NULL
BEGIN
    CREATE DATABASE QAAutomation
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database QAAutomation created.';
END
GO

USE QAAutomation;
GO

-- =============================================================================
-- 2. DROP existing objects (reverse dependency order)
-- =============================================================================

IF OBJECT_ID('dbo.AuditLogs',           'U') IS NOT NULL DROP TABLE dbo.AuditLogs;
IF OBJECT_ID('dbo.TrainingPlanItems',    'U') IS NOT NULL DROP TABLE dbo.TrainingPlanItems;
IF OBJECT_ID('dbo.TrainingPlans',        'U') IS NOT NULL DROP TABLE dbo.TrainingPlans;
IF OBJECT_ID('dbo.HumanReviewItems',     'U') IS NOT NULL DROP TABLE dbo.HumanReviewItems;
IF OBJECT_ID('dbo.SamplingPolicies',     'U') IS NOT NULL DROP TABLE dbo.SamplingPolicies;
IF OBJECT_ID('dbo.CallPipelineItems',    'U') IS NOT NULL DROP TABLE dbo.CallPipelineItems;
IF OBJECT_ID('dbo.CallPipelineJobs',     'U') IS NOT NULL DROP TABLE dbo.CallPipelineJobs;
IF OBJECT_ID('dbo.KnowledgeDocuments',   'U') IS NOT NULL DROP TABLE dbo.KnowledgeDocuments;
IF OBJECT_ID('dbo.KnowledgeSources',     'U') IS NOT NULL DROP TABLE dbo.KnowledgeSources;
IF OBJECT_ID('dbo.EvaluationScores',     'U') IS NOT NULL DROP TABLE dbo.EvaluationScores;
IF OBJECT_ID('dbo.EvaluationResults',    'U') IS NOT NULL DROP TABLE dbo.EvaluationResults;
IF OBJECT_ID('dbo.FormFields',           'U') IS NOT NULL DROP TABLE dbo.FormFields;
IF OBJECT_ID('dbo.FormSections',         'U') IS NOT NULL DROP TABLE dbo.FormSections;
IF OBJECT_ID('dbo.EvaluationForms',      'U') IS NOT NULL DROP TABLE dbo.EvaluationForms;
IF OBJECT_ID('dbo.ParameterClubItems',   'U') IS NOT NULL DROP TABLE dbo.ParameterClubItems;
IF OBJECT_ID('dbo.ParameterClubs',       'U') IS NOT NULL DROP TABLE dbo.ParameterClubs;
IF OBJECT_ID('dbo.RatingLevels',         'U') IS NOT NULL DROP TABLE dbo.RatingLevels;
IF OBJECT_ID('dbo.RatingCriteria',       'U') IS NOT NULL DROP TABLE dbo.RatingCriteria;
IF OBJECT_ID('dbo.Parameters',           'U') IS NOT NULL DROP TABLE dbo.Parameters;
IF OBJECT_ID('dbo.UserProjectAccesses',  'U') IS NOT NULL DROP TABLE dbo.UserProjectAccesses;
IF OBJECT_ID('dbo.Lobs',                 'U') IS NOT NULL DROP TABLE dbo.Lobs;
IF OBJECT_ID('dbo.Projects',             'U') IS NOT NULL DROP TABLE dbo.Projects;
IF OBJECT_ID('dbo.AppUsers',             'U') IS NOT NULL DROP TABLE dbo.AppUsers;
IF OBJECT_ID('dbo.AiConfigs',            'U') IS NOT NULL DROP TABLE dbo.AiConfigs;
GO

-- =============================================================================
-- 3. CREATE TABLES
-- =============================================================================

-- ── AppUsers ──────────────────────────────────────────────────────────────────
CREATE TABLE dbo.AppUsers
(
    Id           INT           IDENTITY(1,1) NOT NULL,
    Username     NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    Email        NVARCHAR(200) NULL,
    Role         NVARCHAR(50)  NOT NULL CONSTRAINT DF_AppUsers_Role DEFAULT ('User'),
    IsActive     BIT           NOT NULL CONSTRAINT DF_AppUsers_IsActive DEFAULT (1),
    CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_AppUsers_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_AppUsers PRIMARY KEY (Id),
    CONSTRAINT UQ_AppUsers_Username UNIQUE (Username)
);
GO

-- ── Projects ──────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Projects
(
    Id                  INT           IDENTITY(1,1) NOT NULL,
    Name                NVARCHAR(200) NOT NULL,
    Description         NVARCHAR(MAX) NULL,
    IsActive            BIT           NOT NULL CONSTRAINT DF_Projects_IsActive DEFAULT (1),
    PiiProtectionEnabled BIT          NOT NULL CONSTRAINT DF_Projects_PiiProt DEFAULT (0),
    PiiRedactionMode    NVARCHAR(20)  NOT NULL CONSTRAINT DF_Projects_PiiMode DEFAULT ('Redact'),
    CreatedAt           DATETIME2     NOT NULL CONSTRAINT DF_Projects_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Projects PRIMARY KEY (Id)
);
GO

-- ── UserProjectAccesses ───────────────────────────────────────────────────────
CREATE TABLE dbo.UserProjectAccesses
(
    Id        INT       IDENTITY(1,1) NOT NULL,
    UserId    INT       NOT NULL,
    ProjectId INT       NOT NULL,
    GrantedAt DATETIME2 NOT NULL CONSTRAINT DF_UPA_GrantedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_UserProjectAccesses PRIMARY KEY (Id),
    CONSTRAINT FK_UPA_User    FOREIGN KEY (UserId)    REFERENCES dbo.AppUsers (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UPA_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE CASCADE
);
GO

-- ── Lobs ─────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Lobs
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    ProjectId   INT           NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Lobs_IsActive DEFAULT (1),
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Lobs_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Lobs PRIMARY KEY (Id),
    CONSTRAINT FK_Lobs_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE CASCADE
);
GO

-- ── EvaluationForms ───────────────────────────────────────────────────────────
CREATE TABLE dbo.EvaluationForms
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_EvalForms_IsActive DEFAULT (1),
    LobId       INT           NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_EvalForms_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2     NOT NULL CONSTRAINT DF_EvalForms_UpdatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_EvaluationForms PRIMARY KEY (Id),
    CONSTRAINT FK_EvalForms_Lob FOREIGN KEY (LobId) REFERENCES dbo.Lobs (Id) ON DELETE SET NULL
);
GO

-- ── FormSections ──────────────────────────────────────────────────────────────
CREATE TABLE dbo.FormSections
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    FormId      INT           NOT NULL,
    Title       NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    [Order]     INT           NOT NULL CONSTRAINT DF_FormSections_Order DEFAULT (0),

    CONSTRAINT PK_FormSections PRIMARY KEY (Id),
    CONSTRAINT FK_FormSections_Form FOREIGN KEY (FormId) REFERENCES dbo.EvaluationForms (Id) ON DELETE CASCADE
);
GO

-- ── FormFields ────────────────────────────────────────────────────────────────
-- FieldType enum: Text=0, TextArea=1, Rating=2, Checkbox=3, Dropdown=4, Number=5
CREATE TABLE dbo.FormFields
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    SectionId   INT           NOT NULL,
    Label       NVARCHAR(200) NOT NULL,
    FieldType   INT           NOT NULL CONSTRAINT DF_FormFields_FieldType DEFAULT (0),
    IsRequired  BIT           NOT NULL CONSTRAINT DF_FormFields_IsRequired DEFAULT (0),
    [Order]     INT           NOT NULL CONSTRAINT DF_FormFields_Order DEFAULT (0),
    Options     NVARCHAR(MAX) NULL,        -- JSON array for Dropdown fields
    MaxRating   INT           NOT NULL CONSTRAINT DF_FormFields_MaxRating DEFAULT (5),
    Description NVARCHAR(MAX) NULL,

    CONSTRAINT PK_FormFields PRIMARY KEY (Id),
    CONSTRAINT FK_FormFields_Section FOREIGN KEY (SectionId) REFERENCES dbo.FormSections (Id) ON DELETE CASCADE
);
GO

-- ── EvaluationResults ─────────────────────────────────────────────────────────
CREATE TABLE dbo.EvaluationResults
(
    Id                   INT           IDENTITY(1,1) NOT NULL,
    FormId               INT           NOT NULL,
    EvaluatedBy          NVARCHAR(200) NOT NULL,
    EvaluatedAt          DATETIME2     NOT NULL CONSTRAINT DF_EvalResults_EvalAt DEFAULT (SYSUTCDATETIME()),
    Notes                NVARCHAR(MAX) NULL,
    AgentName            NVARCHAR(200) NULL,
    CallReference        NVARCHAR(100) NULL,
    CallDate             DATETIME2     NULL,
    CallDurationSeconds  INT           NULL,
    OverallReasoning     NVARCHAR(MAX) NULL,
    SentimentJson        NVARCHAR(MAX) NULL,
    FieldReasoningJson   NVARCHAR(MAX) NULL,

    CONSTRAINT PK_EvaluationResults PRIMARY KEY (Id),
    CONSTRAINT FK_EvalResults_Form FOREIGN KEY (FormId) REFERENCES dbo.EvaluationForms (Id) ON DELETE NO ACTION
);
GO

-- ── EvaluationScores ──────────────────────────────────────────────────────────
CREATE TABLE dbo.EvaluationScores
(
    Id           INT           IDENTITY(1,1) NOT NULL,
    ResultId     INT           NOT NULL,
    FieldId      INT           NOT NULL,
    Value        NVARCHAR(MAX) NOT NULL,
    NumericValue FLOAT         NULL,

    CONSTRAINT PK_EvaluationScores PRIMARY KEY (Id),
    CONSTRAINT FK_EvalScores_Result FOREIGN KEY (ResultId) REFERENCES dbo.EvaluationResults (Id) ON DELETE CASCADE,
    CONSTRAINT FK_EvalScores_Field  FOREIGN KEY (FieldId)  REFERENCES dbo.FormFields (Id) ON DELETE NO ACTION
);
GO

-- ── Parameters ────────────────────────────────────────────────────────────────
CREATE TABLE dbo.Parameters
(
    Id              INT           IDENTITY(1,1) NOT NULL,
    Name            NVARCHAR(200) NOT NULL,
    Description     NVARCHAR(MAX) NULL,
    Category        NVARCHAR(100) NULL,
    DefaultWeight   FLOAT         NOT NULL CONSTRAINT DF_Params_Weight DEFAULT (1.0),
    IsActive        BIT           NOT NULL CONSTRAINT DF_Params_IsActive DEFAULT (1),
    EvaluationType  NVARCHAR(30)  NOT NULL CONSTRAINT DF_Params_EvalType DEFAULT ('LLM'),
    ProjectId       INT           NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_Params_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_Parameters PRIMARY KEY (Id),
    CONSTRAINT FK_Parameters_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE SET NULL
);
GO

-- ── RatingCriteria ────────────────────────────────────────────────────────────
CREATE TABLE dbo.RatingCriteria
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    MinScore    INT           NOT NULL CONSTRAINT DF_RC_MinScore DEFAULT (1),
    MaxScore    INT           NOT NULL CONSTRAINT DF_RC_MaxScore DEFAULT (5),
    IsActive    BIT           NOT NULL CONSTRAINT DF_RC_IsActive DEFAULT (1),
    ProjectId   INT           NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_RC_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_RatingCriteria PRIMARY KEY (Id),
    CONSTRAINT FK_RatingCriteria_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE SET NULL
);
GO

-- ── RatingLevels ─────────────────────────────────────────────────────────────
CREATE TABLE dbo.RatingLevels
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    CriteriaId  INT           NOT NULL,
    Score       INT           NOT NULL,
    Label       NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Color       NVARCHAR(20)  NOT NULL CONSTRAINT DF_RL_Color DEFAULT ('#6c757d'),

    CONSTRAINT PK_RatingLevels PRIMARY KEY (Id),
    CONSTRAINT FK_RatingLevels_Criteria FOREIGN KEY (CriteriaId) REFERENCES dbo.RatingCriteria (Id) ON DELETE CASCADE
);
GO

-- ── ParameterClubs ────────────────────────────────────────────────────────────
CREATE TABLE dbo.ParameterClubs
(
    Id          INT           IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_PC_IsActive DEFAULT (1),
    ProjectId   INT           NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_PC_CreatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_ParameterClubs PRIMARY KEY (Id),
    CONSTRAINT FK_ParameterClubs_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE SET NULL
);
GO

-- ── ParameterClubItems ───────────────────────────────────────────────────────
CREATE TABLE dbo.ParameterClubItems
(
    Id               INT   IDENTITY(1,1) NOT NULL,
    ClubId           INT   NOT NULL,
    ParameterId      INT   NOT NULL,
    [Order]          INT   NOT NULL CONSTRAINT DF_PCI_Order DEFAULT (0),
    WeightOverride   FLOAT NULL,
    RatingCriteriaId INT   NULL,

    CONSTRAINT PK_ParameterClubItems PRIMARY KEY (Id),
    CONSTRAINT FK_PCI_Club      FOREIGN KEY (ClubId)           REFERENCES dbo.ParameterClubs (Id) ON DELETE CASCADE,
    CONSTRAINT FK_PCI_Parameter FOREIGN KEY (ParameterId)      REFERENCES dbo.Parameters (Id) ON DELETE NO ACTION,
    CONSTRAINT FK_PCI_Criteria  FOREIGN KEY (RatingCriteriaId) REFERENCES dbo.RatingCriteria (Id) ON DELETE SET NULL
);
GO

-- ── AiConfigs ────────────────────────────────────────────────────────────────
-- Singleton row (Id = 1)
CREATE TABLE dbo.AiConfigs
(
    Id                  INT           NOT NULL CONSTRAINT DF_AiConfigs_Id DEFAULT (1),
    LlmProvider         NVARCHAR(50)  NOT NULL CONSTRAINT DF_AI_LlmProv DEFAULT ('AzureOpenAI'),
    LlmEndpoint         NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_LlmEP DEFAULT (''),
    LlmApiKey           NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_LlmKey DEFAULT (''),
    LlmDeployment       NVARCHAR(200) NOT NULL CONSTRAINT DF_AI_LlmDep DEFAULT ('gpt-4o'),
    LlmTemperature      REAL          NOT NULL CONSTRAINT DF_AI_LlmTemp DEFAULT (0.1),
    SentimentProvider   NVARCHAR(50)  NOT NULL CONSTRAINT DF_AI_SentProv DEFAULT ('AzureOpenAI'),
    LanguageEndpoint    NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_LangEP DEFAULT (''),
    LanguageApiKey      NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_LangKey DEFAULT (''),
    RagTopK             INT           NOT NULL CONSTRAINT DF_AI_RagTopK DEFAULT (3),
    GoogleApiKey        NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_GKey DEFAULT (''),
    GoogleGeminiModel   NVARCHAR(100) NOT NULL CONSTRAINT DF_AI_GModel DEFAULT ('gemini-1.5-pro'),
    SpeechProvider      NVARCHAR(50)  NOT NULL CONSTRAINT DF_AI_SpeechProv DEFAULT ('Azure'),
    SpeechEndpoint      NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_SpeechEP DEFAULT (''),
    SpeechApiKey        NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AI_SpeechKey DEFAULT (''),
    UpdatedAt           DATETIME2     NOT NULL CONSTRAINT DF_AI_UpdatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_AiConfigs PRIMARY KEY (Id),
    CONSTRAINT CK_AiConfigs_Singleton CHECK (Id = 1)
);
GO

-- ── KnowledgeSources ─────────────────────────────────────────────────────────
CREATE TABLE dbo.KnowledgeSources
(
    Id                    INT           IDENTITY(1,1) NOT NULL,
    Name                  NVARCHAR(200) NOT NULL,
    ConnectorType         NVARCHAR(50)  NOT NULL CONSTRAINT DF_KS_Type DEFAULT ('ManualUpload'),
    Description           NVARCHAR(MAX) NULL,
    BlobConnectionString  NVARCHAR(MAX) NULL,
    BlobContainerName     NVARCHAR(200) NULL,
    SftpHost              NVARCHAR(300) NULL,
    SftpPort              INT           NULL,
    SftpUsername          NVARCHAR(200) NULL,
    SftpPassword          NVARCHAR(MAX) NULL,
    SftpPath              NVARCHAR(500) NULL,
    SharePointSiteUrl     NVARCHAR(MAX) NULL,
    SharePointClientId    NVARCHAR(200) NULL,
    SharePointClientSecret NVARCHAR(MAX) NULL,
    SharePointLibraryName NVARCHAR(200) NULL,
    IsActive              BIT           NOT NULL CONSTRAINT DF_KS_IsActive DEFAULT (1),
    ProjectId             INT           NULL,
    CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_KS_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LastSyncedAt          DATETIME2     NULL,

    CONSTRAINT PK_KnowledgeSources PRIMARY KEY (Id),
    CONSTRAINT FK_KS_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE CASCADE
);
GO

-- ── KnowledgeDocuments ───────────────────────────────────────────────────────
CREATE TABLE dbo.KnowledgeDocuments
(
    Id               INT            IDENTITY(1,1) NOT NULL,
    SourceId         INT            NOT NULL,
    Title            NVARCHAR(500)  NOT NULL,
    FileName         NVARCHAR(1000) NULL,
    Content          NVARCHAR(MAX)  NOT NULL,
    Tags             NVARCHAR(MAX)  NULL,
    ContentSizeBytes BIGINT         NOT NULL CONSTRAINT DF_KD_Size DEFAULT (0),
    UploadedAt       DATETIME2      NOT NULL CONSTRAINT DF_KD_UploadedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_KnowledgeDocuments PRIMARY KEY (Id),
    CONSTRAINT FK_KD_Source FOREIGN KEY (SourceId) REFERENCES dbo.KnowledgeSources (Id) ON DELETE CASCADE
);
GO

-- ── CallPipelineJobs ─────────────────────────────────────────────────────────
CREATE TABLE dbo.CallPipelineJobs
(
    Id                      INT            IDENTITY(1,1) NOT NULL,
    Name                    NVARCHAR(200)  NOT NULL,
    SourceType              NVARCHAR(50)   NOT NULL CONSTRAINT DF_CPJ_SrcType DEFAULT ('BatchUrl'),
    FormId                  INT            NOT NULL,
    ProjectId               INT            NULL,
    Status                  NVARCHAR(20)   NOT NULL CONSTRAINT DF_CPJ_Status DEFAULT ('Pending'),
    CreatedAt               DATETIME2      NOT NULL CONSTRAINT DF_CPJ_CreatedAt DEFAULT (SYSUTCDATETIME()),
    StartedAt               DATETIME2      NULL,
    CompletedAt             DATETIME2      NULL,
    CreatedBy               NVARCHAR(200)  NOT NULL CONSTRAINT DF_CPJ_CreatedBy DEFAULT (''),
    SftpHost                NVARCHAR(MAX)  NULL,
    SftpPort                INT            NULL,
    SftpUsername            NVARCHAR(MAX)  NULL,
    SftpPassword            NVARCHAR(MAX)  NULL,
    SftpPath                NVARCHAR(MAX)  NULL,
    SharePointSiteUrl       NVARCHAR(MAX)  NULL,
    SharePointClientId      NVARCHAR(MAX)  NULL,
    SharePointClientSecret  NVARCHAR(MAX)  NULL,
    SharePointLibraryName   NVARCHAR(MAX)  NULL,
    RecordingPlatformUrl    NVARCHAR(MAX)  NULL,
    RecordingPlatformApiKey NVARCHAR(MAX)  NULL,
    RecordingPlatformTenantId NVARCHAR(MAX) NULL,
    FilterFromDate          NVARCHAR(30)   NULL,
    FilterToDate            NVARCHAR(30)   NULL,
    ErrorMessage            NVARCHAR(MAX)  NULL,

    CONSTRAINT PK_CallPipelineJobs PRIMARY KEY (Id),
    CONSTRAINT FK_CPJ_Form    FOREIGN KEY (FormId)    REFERENCES dbo.EvaluationForms (Id) ON DELETE NO ACTION,
    CONSTRAINT FK_CPJ_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE SET NULL
);
GO

-- ── CallPipelineItems ────────────────────────────────────────────────────────
CREATE TABLE dbo.CallPipelineItems
(
    Id                 INT            IDENTITY(1,1) NOT NULL,
    JobId              INT            NOT NULL,
    SourceReference    NVARCHAR(2000) NULL,
    AgentName          NVARCHAR(200)  NULL,
    CallReference      NVARCHAR(200)  NULL,
    CallDate           DATETIME2      NULL,
    Status             NVARCHAR(20)   NOT NULL CONSTRAINT DF_CPI_Status DEFAULT ('Pending'),
    CreatedAt          DATETIME2      NOT NULL CONSTRAINT DF_CPI_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ProcessedAt        DATETIME2      NULL,
    ErrorMessage       NVARCHAR(MAX)  NULL,
    EvaluationResultId INT            NULL,
    ScorePercent       FLOAT          NULL,
    AiReasoning        NVARCHAR(MAX)  NULL,

    CONSTRAINT PK_CallPipelineItems PRIMARY KEY (Id),
    CONSTRAINT FK_CPI_Job    FOREIGN KEY (JobId)              REFERENCES dbo.CallPipelineJobs (Id) ON DELETE CASCADE,
    CONSTRAINT FK_CPI_Result FOREIGN KEY (EvaluationResultId) REFERENCES dbo.EvaluationResults (Id) ON DELETE SET NULL
);
GO

-- ── SamplingPolicies ─────────────────────────────────────────────────────────
CREATE TABLE dbo.SamplingPolicies
(
    Id                  INT           IDENTITY(1,1) NOT NULL,
    Name                NVARCHAR(200) NOT NULL,
    Description         NVARCHAR(500) NULL,
    ProjectId           INT           NULL,
    CallTypeFilter      NVARCHAR(200) NULL,
    MinDurationSeconds  INT           NULL,
    MaxDurationSeconds  INT           NULL,
    SamplingMethod      NVARCHAR(20)  NOT NULL CONSTRAINT DF_SP_Method DEFAULT ('Percentage'),
    SampleValue         REAL          NOT NULL CONSTRAINT DF_SP_Value DEFAULT (10),
    IsActive            BIT           NOT NULL CONSTRAINT DF_SP_IsActive DEFAULT (1),
    CreatedBy           NVARCHAR(200) NOT NULL CONSTRAINT DF_SP_CreatedBy DEFAULT (''),
    CreatedAt           DATETIME2     NOT NULL CONSTRAINT DF_SP_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2     NOT NULL CONSTRAINT DF_SP_UpdatedAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_SamplingPolicies PRIMARY KEY (Id),
    CONSTRAINT FK_SP_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects (Id) ON DELETE SET NULL
);
GO

-- ── HumanReviewItems ─────────────────────────────────────────────────────────
CREATE TABLE dbo.HumanReviewItems
(
    Id                 INT           IDENTITY(1,1) NOT NULL,
    EvaluationResultId INT           NOT NULL,
    SamplingPolicyId   INT           NULL,
    SampledAt          DATETIME2     NOT NULL CONSTRAINT DF_HRI_SampledAt DEFAULT (SYSUTCDATETIME()),
    SampledBy          NVARCHAR(200) NOT NULL CONSTRAINT DF_HRI_SampledBy DEFAULT ('system'),
    AssignedTo         NVARCHAR(200) NULL,
    Status             NVARCHAR(20)  NOT NULL CONSTRAINT DF_HRI_Status DEFAULT ('Pending'),
    ReviewerComment    NVARCHAR(MAX) NULL,
    ReviewVerdict      NVARCHAR(20)  NULL,
    ReviewedBy         NVARCHAR(200) NULL,
    ReviewedAt         DATETIME2     NULL,

    CONSTRAINT PK_HumanReviewItems PRIMARY KEY (Id),
    CONSTRAINT FK_HRI_EvalResult     FOREIGN KEY (EvaluationResultId) REFERENCES dbo.EvaluationResults (Id) ON DELETE CASCADE,
    CONSTRAINT FK_HRI_SamplingPolicy FOREIGN KEY (SamplingPolicyId)   REFERENCES dbo.SamplingPolicies (Id) ON DELETE SET NULL
);
GO

-- ── TrainingPlans ─────────────────────────────────────────────────────────────
CREATE TABLE dbo.TrainingPlans
(
    Id                  INT           IDENTITY(1,1) NOT NULL,
    Title               NVARCHAR(300) NOT NULL,
    Description         NVARCHAR(MAX) NULL,
    AgentName           NVARCHAR(200) NOT NULL,
    AgentUsername       NVARCHAR(200) NULL,
    TrainerName         NVARCHAR(200) NOT NULL,
    TrainerUsername     NVARCHAR(200) NULL,
    Status              NVARCHAR(20)  NOT NULL CONSTRAINT DF_TP_Status DEFAULT ('Draft'),
    DueDate             DATETIME2     NULL,
    ProjectId           INT           NULL,
    EvaluationResultId  INT           NULL,
    HumanReviewItemId   INT           NULL,
    CreatedBy           NVARCHAR(200) NOT NULL CONSTRAINT DF_TP_CreatedBy DEFAULT (''),
    CreatedAt           DATETIME2     NOT NULL CONSTRAINT DF_TP_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt           DATETIME2     NOT NULL CONSTRAINT DF_TP_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    ClosedBy            NVARCHAR(200) NULL,
    ClosedAt            DATETIME2     NULL,
    ClosingNotes        NVARCHAR(MAX) NULL,

    CONSTRAINT PK_TrainingPlans PRIMARY KEY (Id),
    CONSTRAINT FK_TP_Project         FOREIGN KEY (ProjectId)          REFERENCES dbo.Projects (Id) ON DELETE SET NULL,
    CONSTRAINT FK_TP_EvalResult      FOREIGN KEY (EvaluationResultId) REFERENCES dbo.EvaluationResults (Id) ON DELETE SET NULL,
    CONSTRAINT FK_TP_HumanReview     FOREIGN KEY (HumanReviewItemId)  REFERENCES dbo.HumanReviewItems (Id) ON DELETE NO ACTION
);
GO

-- ── TrainingPlanItems ─────────────────────────────────────────────────────────
CREATE TABLE dbo.TrainingPlanItems
(
    Id               INT           IDENTITY(1,1) NOT NULL,
    TrainingPlanId   INT           NOT NULL,
    TargetArea       NVARCHAR(200) NOT NULL,
    ItemType         NVARCHAR(30)  NOT NULL CONSTRAINT DF_TPI_Type DEFAULT ('Observation'),
    Content          NVARCHAR(MAX) NOT NULL,
    Status           NVARCHAR(20)  NOT NULL CONSTRAINT DF_TPI_Status DEFAULT ('Pending'),
    [Order]          INT           NOT NULL CONSTRAINT DF_TPI_Order DEFAULT (0),
    CompletedBy      NVARCHAR(200) NULL,
    CompletedAt      DATETIME2     NULL,
    CompletionNotes  NVARCHAR(MAX) NULL,

    CONSTRAINT PK_TrainingPlanItems PRIMARY KEY (Id),
    CONSTRAINT FK_TPI_Plan FOREIGN KEY (TrainingPlanId) REFERENCES dbo.TrainingPlans (Id) ON DELETE CASCADE
);
GO

-- ── AuditLogs ─────────────────────────────────────────────────────────────────
CREATE TABLE dbo.AuditLogs
(
    Id              INT            IDENTITY(1,1) NOT NULL,
    ProjectId       INT            NULL,
    Category        NVARCHAR(30)   NOT NULL,
    EventType       NVARCHAR(50)   NOT NULL,
    Outcome         NVARCHAR(20)   NOT NULL,
    Actor           NVARCHAR(200)  NULL,
    PiiTypesDetected NVARCHAR(500) NULL,
    HttpMethod      NVARCHAR(10)   NULL,
    Endpoint        NVARCHAR(1000) NULL,
    HttpStatusCode  INT            NULL,
    DurationMs      BIGINT         NULL,
    Provider        NVARCHAR(100)  NULL,
    Details         NVARCHAR(2000) NULL,
    OccurredAt      DATETIME2      NOT NULL CONSTRAINT DF_AL_OccurredAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_AuditLogs PRIMARY KEY (Id)
    -- No FK to Projects — AuditLog is append-only and must survive project deletion
);
GO

-- =============================================================================
-- 4. INDEXES
-- =============================================================================

-- AppUsers
CREATE UNIQUE INDEX IX_AppUsers_Username ON dbo.AppUsers (Username);

-- Lobs
CREATE INDEX IX_Lobs_ProjectId ON dbo.Lobs (ProjectId);

-- EvaluationForms
CREATE INDEX IX_EvalForms_LobId ON dbo.EvaluationForms (LobId);

-- FormSections
CREATE INDEX IX_FormSections_FormId ON dbo.FormSections (FormId);

-- FormFields
CREATE INDEX IX_FormFields_SectionId ON dbo.FormFields (SectionId);

-- EvaluationResults
CREATE INDEX IX_EvalResults_FormId     ON dbo.EvaluationResults (FormId);
CREATE INDEX IX_EvalResults_AgentName  ON dbo.EvaluationResults (AgentName);
CREATE INDEX IX_EvalResults_EvalAt     ON dbo.EvaluationResults (EvaluatedAt DESC);

-- EvaluationScores
CREATE INDEX IX_EvalScores_ResultId ON dbo.EvaluationScores (ResultId);

-- Parameters
CREATE INDEX IX_Parameters_ProjectId ON dbo.Parameters (ProjectId);

-- RatingCriteria
CREATE INDEX IX_RatingCriteria_ProjectId ON dbo.RatingCriteria (ProjectId);

-- CallPipelineJobs
CREATE INDEX IX_CPJ_ProjectId  ON dbo.CallPipelineJobs (ProjectId);
CREATE INDEX IX_CPJ_Status     ON dbo.CallPipelineJobs (Status);
CREATE INDEX IX_CPJ_CreatedAt  ON dbo.CallPipelineJobs (CreatedAt DESC);

-- CallPipelineItems
CREATE INDEX IX_CPI_JobId  ON dbo.CallPipelineItems (JobId);
CREATE INDEX IX_CPI_Status ON dbo.CallPipelineItems (Status);

-- HumanReviewItems
CREATE INDEX IX_HRI_Status     ON dbo.HumanReviewItems (Status);
CREATE INDEX IX_HRI_AssignedTo ON dbo.HumanReviewItems (AssignedTo);

-- TrainingPlans
CREATE INDEX IX_TP_ProjectId ON dbo.TrainingPlans (ProjectId);
CREATE INDEX IX_TP_Status    ON dbo.TrainingPlans (Status);

-- AuditLogs
CREATE INDEX IX_AL_OccurredAt  ON dbo.AuditLogs (OccurredAt DESC);
CREATE INDEX IX_AL_ProjectId   ON dbo.AuditLogs (ProjectId);
CREATE INDEX IX_AL_Category    ON dbo.AuditLogs (Category);
CREATE INDEX IX_AL_EventType   ON dbo.AuditLogs (EventType);
GO

-- =============================================================================
-- 5. SEED DATA
-- =============================================================================

-- ── 5.1  AiConfigs (singleton) ────────────────────────────────────────────────
INSERT INTO dbo.AiConfigs
    (Id, LlmProvider, LlmEndpoint, LlmApiKey, LlmDeployment, LlmTemperature,
     SentimentProvider, LanguageEndpoint, LanguageApiKey, RagTopK,
     GoogleApiKey, GoogleGeminiModel, SpeechProvider, SpeechEndpoint, SpeechApiKey, UpdatedAt)
VALUES
    (1, 'AzureOpenAI', '', '', 'gpt-4o', 0.1,
     'AzureOpenAI', '', '', 3,
     '', 'gemini-1.5-pro', 'Azure', '', '', SYSUTCDATETIME());
GO

-- ── 5.2  AppUsers ────────────────────────────────────────────────────────────
-- Passwords are SHA-256 hex hashes (matches AppDbContext.HashPassword).
-- admin     → password: Admin@123
-- qamanager → password: QA@Manager1
-- analyst1  → password: Analyst@1
-- trainer1  → password: Trainer@1
-- agent1    → password: Agent@1
INSERT INTO dbo.AppUsers (Username, PasswordHash, Email, Role, IsActive, CreatedAt)
VALUES
    ('admin',
     'e86f78a8a3caf0b60d8e74e5942aa6d86dc150cd3c03338aef25b7d2d7e3acc7',
     'admin@qaplatform.local', 'Admin', 1, '2026-01-01 00:00:00'),
    ('qamanager',
     '7aff26f8a5aa7ef020f0efeb1c747c1563df51f6fad025741d06a99b3259a7b6',
     'qamanager@qaplatform.local', 'Manager', 1, '2026-01-01 00:00:00'),
    ('analyst1',
     '7657ceb01ad1eaa5f98bacf3bd1e78d7e30ee70115b306fa5d6b86eaec5f9a8b',
     'analyst1@qaplatform.local', 'Analyst', 1, '2026-01-02 00:00:00'),
    ('trainer1',
     '83b0e6ae5824d14e339b97119776f11bae629890e5fa518ef3f587b6919c0a08',
     'trainer1@qaplatform.local', 'User', 1, '2026-01-02 00:00:00'),
    ('agent1',
     '82edae304d5ac65434f7c04a85656610ded207660c43107b092157d71ce12f5c',
     'agent1@qaplatform.local', 'User', 1, '2026-01-02 00:00:00');
GO

-- ── 5.3  Projects ────────────────────────────────────────────────────────────
INSERT INTO dbo.Projects (Name, Description, IsActive, PiiProtectionEnabled, PiiRedactionMode, CreatedAt)
VALUES
    ('Capital One',
     'Capital One Financial Corporation',
     1, 1, 'Redact', '2026-01-05 00:00:00'),
    ('Youtube',
     'YouTube Creator Support Operations — Internal Quality Assurance',
     1, 0, 'Redact', '2026-01-05 00:00:00');
GO

-- ── 5.4  UserProjectAccesses ──────────────────────────────────────────────────
-- admin/qamanager/analyst1 get access to all projects; trainer1/agent1 only Capital One
INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, p.Id, '2026-01-06 00:00:00'
FROM   dbo.AppUsers u
CROSS JOIN dbo.Projects p
WHERE  u.Username IN ('admin','qamanager','analyst1');

INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, p.Id, '2026-01-06 00:00:00'
FROM   dbo.AppUsers u
JOIN   dbo.Projects p ON p.Name = 'Capital One'
WHERE  u.Username IN ('trainer1','agent1');
GO

-- ── 5.5  LOBs ────────────────────────────────────────────────────────────────
DECLARE @PCapOne  INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @PYoutube INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Youtube');

INSERT INTO dbo.Lobs (ProjectId, Name, Description, IsActive, CreatedAt)
VALUES
    (@PCapOne,  'Customer Support Call', 'Customer support call centre quality evaluation', 1, '2026-01-10 00:00:00'),
    (@PYoutube, 'CSO',                   'Creator Support Operations line of business',     1, '2026-01-10 00:00:00');
GO

-- ── 5.6  Parameters ──────────────────────────────────────────────────────────
DECLARE @PCapOne2  INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @PYoutube2 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Youtube');

INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
VALUES
    -- Capital One: Call Opening
    ('Professional Greeting',
     'Agent uses approved greeting script with brand name, own name, and offer to help',
     'Call Opening', 1.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Customer Identity Verification',
     'Completes full CID verification per PCI/security policy before discussing account',
     'Call Opening', 2.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Brand Introduction',
     'Sets the right tone and properly introduces Capital One services',
     'Call Opening', 1.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    -- Capital One: Issue Resolution
    ('First Call Resolution',
     'Resolves customer issue completely without need for callback or transfer',
     'Issue Resolution', 3.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Product & Policy Knowledge',
     'Demonstrates accurate knowledge of Capital One credit card products, rates, and policies',
     'Issue Resolution', 2.0, 1, 'KnowledgeBased', @PCapOne2, '2026-01-08 00:00:00'),
    ('Information Accuracy',
     'All information provided to customer is accurate and up to date',
     'Issue Resolution', 2.5, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Problem-Solving Ability',
     'Effectively identifies root cause and provides appropriate resolution or alternatives',
     'Issue Resolution', 2.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    -- Capital One: Communication Skills
    ('Verbal Clarity & Articulation',
     'Speaks clearly, avoids jargon, adjusts language to customer''s level',
     'Communication Skills', 1.5, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Active Listening',
     'Demonstrates understanding, does not interrupt, confirms understanding before proceeding',
     'Communication Skills', 1.5, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Empathy & Rapport Building',
     'Acknowledges customer emotions, personalizes the interaction, builds trust',
     'Communication Skills', 2.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Pace, Tone & Energy',
     'Maintains professional tone throughout, appropriate pace, positive energy',
     'Communication Skills', 1.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    -- Capital One: Compliance & Procedures
    ('CFPB Regulatory Compliance',
     'Adheres to all CFPB regulations including fair lending, UDAAP, and debt collection rules',
     'Compliance & Procedures', 5.0, 1, 'KnowledgeBased', @PCapOne2, '2026-01-08 00:00:00'),
    ('Required Disclosures',
     'Provides all mandatory disclosures (APR, fees, payment terms) as required by Reg Z',
     'Compliance & Procedures', 3.0, 1, 'KnowledgeBased', @PCapOne2, '2026-01-08 00:00:00'),
    ('PCI Data Security',
     'Does not capture, repeat, or store sensitive payment card data in violation of PCI DSS',
     'Compliance & Procedures', 5.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    -- Capital One: Call Closing
    ('Issue Summary & Confirmation',
     'Summarizes resolution and confirms customer satisfaction before closing',
     'Call Closing', 1.5, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Offer of Further Assistance',
     'Proactively asks if customer needs anything else before ending the call',
     'Call Closing', 1.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    ('Professional Sign-Off',
     'Uses approved closing script, thanks customer, and ends call professionally',
     'Call Closing', 1.0, 1, 'LLM', @PCapOne2, '2026-01-08 00:00:00'),
    -- Youtube: Creator Critical — Effectiveness
    ('Accuracy',
     'Did the creator receive an accurate and complete solution for all the informed issues?',
     'Creator Critical — Effectiveness', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Tailoring',
     'Were the issues or expectations of the creator met with the right level of personalisation?',
     'Creator Critical — Effectiveness', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Obviation & Next Steps',
     'Has the creator been equipped with relevant obviation opportunities and next steps?',
     'Creator Critical — Effectiveness', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    -- Youtube: Creator Critical — Effort
    ('Responsiveness',
     'Have we set and/or kept expectations with regards to timely and proactive follow-up communications?',
     'Creator Critical — Effort', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Internal Coordination',
     'Did we reduce creator effort by effectively connecting them with the right internal teams (consults and bugs)?',
     'Creator Critical — Effort', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Workflows Adherence',
     'Did we minimise creator effort by following correct workflows?',
     'Creator Critical — Effort', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Creator Feedback',
     'Was the creator reassured that their feedback was captured and addressed?',
     'Creator Critical — Effort', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('CSAT Survey',
     'Was the creator appropriately asked to provide feedback through a CSAT survey?',
     'Creator Critical — Effort', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    -- Youtube: Creator Critical — Engagement
    ('Clarity',
     'Has the creator received clear communication through the use of correct language and effective questioning?',
     'Creator Critical — Engagement', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Empathy',
     'Was the creator reassured that there was a clear understanding of the goal or problem, urgency and sensitivities?',
     'Creator Critical — Engagement', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Tone',
     'Did the creator receive consistently professional and respectful communications aligned with YouTube Tone & Voice guidelines?',
     'Creator Critical — Engagement', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    -- Youtube: Business Critical
    ('Due Diligence',
     'Did the agent complete all required due-diligence steps before responding or escalating?',
     'Business Critical', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Issue Tagging',
     'Was the case correctly tagged / categorised using Neo Categorization?',
     'Business Critical', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    -- Youtube: Compliance Critical
    ('Authentication',
     'Did the agent follow the correct authentication process before discussing account or creator details?',
     'Compliance Critical', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Keep YouTube Safe',
     'Did the agent adhere to all policies that keep YouTube and its creators safe (trust & safety, content policy)?',
     'Compliance Critical', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00'),
    ('Policy',
     'Did the agent correctly apply and communicate YouTube policies relevant to the creator''s issue?',
     'Compliance Critical', 1.0, 1, 'LLM', @PYoutube2, '2026-01-08 00:00:00');
GO

-- ── 5.7  RatingCriteria & Levels ────────────────────────────────────────────
DECLARE @PCapOne3  INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @PYoutube3 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Youtube');

INSERT INTO dbo.RatingCriteria (Name, Description, MinScore, MaxScore, IsActive, ProjectId, CreatedAt)
VALUES
    ('QA Score (1-5)',
     'Standard quality score from 1 (Unacceptable) to 5 (Outstanding)',
     1, 5, 1, @PCapOne3, '2026-01-08 00:00:00'),
    ('Compliance (Pass/Fail)',
     'Binary compliance check - failure is auto-fail',
     0, 1, 1, @PCapOne3, '2026-01-08 00:00:00'),
    ('YouTube IQA — Pass/Fail',
     'Non-compensatory pass/fail used across all YouTube IQA competencies. Failure in any mandatory competency results in auto-fail for that category.',
     0, 1, 1, @PYoutube3, '2026-01-08 00:00:00');
GO

DECLARE @RCQa   INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'QA Score (1-5)');
DECLARE @RCComp INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'Compliance (Pass/Fail)');
DECLARE @RCYt   INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'YouTube IQA — Pass/Fail');

INSERT INTO dbo.RatingLevels (CriteriaId, Score, Label, Description, Color)
VALUES
    -- QA Score (1-5)
    (@RCQa, 1, 'Unacceptable',      'Critical failure affecting customer or compliance',    '#dc3545'),
    (@RCQa, 2, 'Needs Improvement', 'Below standard performance',                           '#fd7e14'),
    (@RCQa, 3, 'Meets Standard',    'Satisfactory performance meeting expectations',        '#ffc107'),
    (@RCQa, 4, 'Exceeds Standard',  'Above average performance',                           '#20c997'),
    (@RCQa, 5, 'Outstanding',       'Exemplary performance, exceeded all expectations',     '#198754'),
    -- Compliance (Pass/Fail)
    (@RCComp, 0, 'FAIL', 'Non-compliant — requires immediate coaching',                    '#dc3545'),
    (@RCComp, 1, 'PASS', 'Compliant with policy and regulation',                           '#198754'),
    -- YouTube IQA — Pass/Fail
    (@RCYt, 0, 'FAIL', 'Competency not met — triggers category auto-fail',                 '#dc3545'),
    (@RCYt, 1, 'PASS', 'Competency met or not applicable (Yes/NA)',                        '#198754');
GO

-- ── 5.8  ParameterClubs ──────────────────────────────────────────────────────
DECLARE @PCapOne4  INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @PYoutube4 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Youtube');

INSERT INTO dbo.ParameterClubs (Name, Description, IsActive, ProjectId, CreatedAt)
VALUES
    -- Capital One clubs
    ('Call Opening',
     'Initial call handling — greeting, verification, and brand introduction',
     1, @PCapOne4, '2026-01-09 00:00:00'),
    ('Issue Resolution',
     'Effectiveness in understanding and resolving the customer''s credit card issue',
     1, @PCapOne4, '2026-01-09 00:00:00'),
    ('Communication Skills',
     'Quality of verbal communication and relationship building',
     1, @PCapOne4, '2026-01-09 00:00:00'),
    ('Compliance & Procedures',
     'Adherence to regulatory and internal compliance requirements — violations are auto-fail',
     1, @PCapOne4, '2026-01-09 00:00:00'),
    ('Call Closing',
     'Professional and thorough call conclusion',
     1, @PCapOne4, '2026-01-09 00:00:00'),
    -- Youtube clubs
    ('Creator Critical – Effectiveness',
     'Measures whether the creator received an accurate, tailored, and complete solution including relevant next steps.',
     1, @PYoutube4, '2026-01-09 00:00:00'),
    ('Creator Critical – Effort',
     'Measures how much effort was required for the creator to reach resolution, covering responsiveness, coordination, workflows, and feedback loops.',
     1, @PYoutube4, '2026-01-09 00:00:00'),
    ('Creator Critical – Engagement',
     'Measures how the creator felt during the interaction in terms of communication clarity, empathy, and professional tone.',
     1, @PYoutube4, '2026-01-09 00:00:00'),
    ('Business Critical',
     'Non-compensatory business-critical checks: due diligence and correct issue tagging.',
     1, @PYoutube4, '2026-01-09 00:00:00'),
    ('Compliance Critical',
     'Non-compensatory compliance checks: authentication, trust & safety, and policy adherence.',
     1, @PYoutube4, '2026-01-09 00:00:00');
GO

-- ParameterClubItems
DECLARE @PCapOne4b  INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @PYoutube4b INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Youtube');
DECLARE @RCQa2   INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'QA Score (1-5)');
DECLARE @RCComp2 INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'Compliance (Pass/Fail)');
DECLARE @RCYt2   INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'YouTube IQA — Pass/Fail');
DECLARE @CoClub1 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Call Opening'            AND ProjectId = @PCapOne4b);
DECLARE @CoClub2 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Issue Resolution'        AND ProjectId = @PCapOne4b);
DECLARE @CoClub3 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Communication Skills'    AND ProjectId = @PCapOne4b);
DECLARE @CoClub4 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Compliance & Procedures' AND ProjectId = @PCapOne4b);
DECLARE @CoClub5 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Call Closing'            AND ProjectId = @PCapOne4b);
DECLARE @YtClub1 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Creator Critical – Effectiveness' AND ProjectId = @PYoutube4b);
DECLARE @YtClub2 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Creator Critical – Effort'        AND ProjectId = @PYoutube4b);
DECLARE @YtClub3 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Creator Critical – Engagement'    AND ProjectId = @PYoutube4b);
DECLARE @YtClub4 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Business Critical'                AND ProjectId = @PYoutube4b);
DECLARE @YtClub5 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Compliance Critical'              AND ProjectId = @PYoutube4b);

INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], RatingCriteriaId)
VALUES
    -- Call Opening
    (@CoClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Professional Greeting'          AND ProjectId=@PCapOne4b), 0, @RCQa2),
    (@CoClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Customer Identity Verification' AND ProjectId=@PCapOne4b), 1, @RCComp2),
    (@CoClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Brand Introduction'             AND ProjectId=@PCapOne4b), 2, @RCQa2),
    -- Issue Resolution
    (@CoClub2, (SELECT Id FROM dbo.Parameters WHERE Name='First Call Resolution'          AND ProjectId=@PCapOne4b), 0, @RCQa2),
    (@CoClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Product & Policy Knowledge'     AND ProjectId=@PCapOne4b), 1, @RCQa2),
    (@CoClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Information Accuracy'           AND ProjectId=@PCapOne4b), 2, @RCQa2),
    (@CoClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Problem-Solving Ability'        AND ProjectId=@PCapOne4b), 3, @RCQa2),
    -- Communication Skills
    (@CoClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Verbal Clarity & Articulation'  AND ProjectId=@PCapOne4b), 0, @RCQa2),
    (@CoClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Active Listening'               AND ProjectId=@PCapOne4b), 1, @RCQa2),
    (@CoClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Empathy & Rapport Building'     AND ProjectId=@PCapOne4b), 2, @RCQa2),
    (@CoClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Pace, Tone & Energy'            AND ProjectId=@PCapOne4b), 3, @RCQa2),
    -- Compliance & Procedures
    (@CoClub4, (SELECT Id FROM dbo.Parameters WHERE Name='CFPB Regulatory Compliance'     AND ProjectId=@PCapOne4b), 0, @RCComp2),
    (@CoClub4, (SELECT Id FROM dbo.Parameters WHERE Name='Required Disclosures'           AND ProjectId=@PCapOne4b), 1, @RCComp2),
    (@CoClub4, (SELECT Id FROM dbo.Parameters WHERE Name='PCI Data Security'              AND ProjectId=@PCapOne4b), 2, @RCComp2),
    -- Call Closing
    (@CoClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Issue Summary & Confirmation'   AND ProjectId=@PCapOne4b), 0, @RCQa2),
    (@CoClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Offer of Further Assistance'    AND ProjectId=@PCapOne4b), 1, @RCQa2),
    (@CoClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Professional Sign-Off'          AND ProjectId=@PCapOne4b), 2, @RCQa2),
    -- Creator Critical – Effectiveness
    (@YtClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Accuracy'               AND ProjectId=@PYoutube4b), 0, @RCYt2),
    (@YtClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Tailoring'              AND ProjectId=@PYoutube4b), 1, @RCYt2),
    (@YtClub1, (SELECT Id FROM dbo.Parameters WHERE Name='Obviation & Next Steps' AND ProjectId=@PYoutube4b), 2, @RCYt2),
    -- Creator Critical – Effort
    (@YtClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Responsiveness'        AND ProjectId=@PYoutube4b), 0, @RCYt2),
    (@YtClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Internal Coordination' AND ProjectId=@PYoutube4b), 1, @RCYt2),
    (@YtClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Workflows Adherence'   AND ProjectId=@PYoutube4b), 2, @RCYt2),
    (@YtClub2, (SELECT Id FROM dbo.Parameters WHERE Name='Creator Feedback'      AND ProjectId=@PYoutube4b), 3, @RCYt2),
    (@YtClub2, (SELECT Id FROM dbo.Parameters WHERE Name='CSAT Survey'           AND ProjectId=@PYoutube4b), 4, @RCYt2),
    -- Creator Critical – Engagement
    (@YtClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Clarity' AND ProjectId=@PYoutube4b), 0, @RCYt2),
    (@YtClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Empathy' AND ProjectId=@PYoutube4b), 1, @RCYt2),
    (@YtClub3, (SELECT Id FROM dbo.Parameters WHERE Name='Tone'    AND ProjectId=@PYoutube4b), 2, @RCYt2),
    -- Business Critical
    (@YtClub4, (SELECT Id FROM dbo.Parameters WHERE Name='Due Diligence' AND ProjectId=@PYoutube4b), 0, @RCYt2),
    (@YtClub4, (SELECT Id FROM dbo.Parameters WHERE Name='Issue Tagging'  AND ProjectId=@PYoutube4b), 1, @RCYt2),
    -- Compliance Critical
    (@YtClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Authentication'    AND ProjectId=@PYoutube4b), 0, @RCYt2),
    (@YtClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Keep YouTube Safe' AND ProjectId=@PYoutube4b), 1, @RCYt2),
    (@YtClub5, (SELECT Id FROM dbo.Parameters WHERE Name='Policy'            AND ProjectId=@PYoutube4b), 2, @RCYt2);
GO

-- ── 5.9  Evaluation Forms ─────────────────────────────────────────────────────
DECLARE @LobCS  INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'Customer Support Call');
DECLARE @LobCSO INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'CSO');

INSERT INTO dbo.EvaluationForms (Name, Description, IsActive, LobId, CreatedAt, UpdatedAt)
VALUES
    ('Capital One — Credit Card Customer Support QA Form',
     'Quality evaluation form for Capital One credit card customer support interactions. Covers call handling, issue resolution, communication, compliance, and closing.',
     1, @LobCS,  '2026-01-12 00:00:00', '2026-01-12 00:00:00'),
    ('YouTube CSO IQA Evaluation Form',
     'Internal Quality Assurance evaluation form for YouTube Creator Support Operations. Based on the YouTube CSO QA Framework covering Effectiveness, Effort, Engagement, Business Critical, and Compliance Critical competencies.',
     1, @LobCSO, '2026-01-12 00:00:00', '2026-01-12 00:00:00');
GO

-- ── 5.9a  Form Sections & Fields — Capital One QA Form ───────────────────────
DECLARE @FormCO INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Capital One — Credit Card Customer Support QA Form');
DECLARE @COSec0 INT, @COSec1 INT, @COSec2 INT, @COSec3 INT, @COSec4 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@FormCO, 'Call Opening',            'Initial call handling — greeting, verification, and brand introduction',                  0),
    (@FormCO, 'Issue Resolution',        'Effectiveness in understanding and resolving the customer''s credit card issue',          1),
    (@FormCO, 'Communication Skills',    'Quality of verbal communication and relationship building',                               2),
    (@FormCO, 'Compliance & Procedures', 'Adherence to regulatory and internal compliance requirements — violations are auto-fail', 3),
    (@FormCO, 'Call Closing',            'Professional and thorough call conclusion',                                               4);

SET @COSec4 = SCOPE_IDENTITY();
SET @COSec0 = @COSec4 - 4;
SET @COSec1 = @COSec0 + 1;
SET @COSec2 = @COSec0 + 2;
SET @COSec3 = @COSec0 + 3;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating)
VALUES
    -- Call Opening (FieldType=2 => Rating)
    (@COSec0, 'Professional Greeting',          2, 1, 0, 5),
    (@COSec0, 'Customer Identity Verification', 2, 1, 1, 1),
    (@COSec0, 'Brand Introduction',             2, 0, 2, 5),
    -- Issue Resolution
    (@COSec1, 'First Call Resolution',          2, 1, 0, 5),
    (@COSec1, 'Product & Policy Knowledge',     2, 0, 1, 5),
    (@COSec1, 'Information Accuracy',           2, 1, 2, 5),
    (@COSec1, 'Problem-Solving Ability',        2, 0, 3, 5),
    -- Communication Skills
    (@COSec2, 'Verbal Clarity & Articulation',  2, 0, 0, 5),
    (@COSec2, 'Active Listening',               2, 0, 1, 5),
    (@COSec2, 'Empathy & Rapport Building',     2, 0, 2, 5),
    (@COSec2, 'Pace, Tone & Energy',            2, 0, 3, 5),
    -- Compliance & Procedures
    (@COSec3, 'CFPB Regulatory Compliance',     2, 1, 0, 1),
    (@COSec3, 'Required Disclosures',           2, 1, 1, 1),
    (@COSec3, 'PCI Data Security',              2, 1, 2, 1),
    -- Call Closing
    (@COSec4, 'Issue Summary & Confirmation',   2, 0, 0, 5),
    (@COSec4, 'Offer of Further Assistance',    2, 0, 1, 5),
    (@COSec4, 'Professional Sign-Off',          2, 0, 2, 5);
GO

-- ── 5.9b  Form Sections & Fields — YouTube CSO IQA Form ─────────────────────
DECLARE @FormYT INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'YouTube CSO IQA Evaluation Form');
DECLARE @YTSec0 INT, @YTSec1 INT, @YTSec2 INT, @YTSec3 INT, @YTSec4 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@FormYT, 'Creator Critical - Effectiveness', 'Have we helped the creator with their goal/issue?',                                                              0),
    (@FormYT, 'Creator Critical - Effort',         'How much effort was it for the creator to get a resolution?',                                                    1),
    (@FormYT, 'Creator Critical - Engagement',     'How did we make the creator feel during their interaction?',                                                     2),
    (@FormYT, 'Business Critical',                 'Non-compensatory business-critical competencies — failure in any one triggers an auto-fail for this category.',  3),
    (@FormYT, 'Compliance Critical',               'Non-compensatory compliance competencies — failure in any one triggers an auto-fail for this category.',          4);

SET @YTSec4 = SCOPE_IDENTITY();
SET @YTSec0 = @YTSec4 - 4;
SET @YTSec1 = @YTSec0 + 1;
SET @YTSec2 = @YTSec0 + 2;
SET @YTSec3 = @YTSec0 + 3;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating)
VALUES
    -- Creator Critical - Effectiveness
    (@YTSec0, 'Accuracy',               2, 1, 0, 1),
    (@YTSec0, 'Tailoring',              2, 1, 1, 1),
    (@YTSec0, 'Obviation & Next Steps', 2, 1, 2, 1),
    -- Creator Critical - Effort
    (@YTSec1, 'Responsiveness',         2, 1, 0, 1),
    (@YTSec1, 'Internal Coordination',  2, 1, 1, 1),
    (@YTSec1, 'Workflows Adherence',    2, 1, 2, 1),
    (@YTSec1, 'Creator Feedback',       2, 1, 3, 1),
    (@YTSec1, 'CSAT Survey',            2, 0, 4, 1),
    -- Creator Critical - Engagement
    (@YTSec2, 'Clarity',                2, 1, 0, 1),
    (@YTSec2, 'Empathy',                2, 1, 1, 1),
    (@YTSec2, 'Tone',                   2, 1, 2, 1),
    -- Business Critical
    (@YTSec3, 'Due Diligence',          2, 1, 0, 1),
    (@YTSec3, 'Issue Tagging',          2, 1, 1, 1),
    -- Compliance Critical
    (@YTSec4, 'Authentication',         2, 1, 0, 1),
    (@YTSec4, 'Keep YouTube Safe',      2, 1, 1, 1),
    (@YTSec4, 'Policy',                 2, 1, 2, 1);
GO

-- ── 5.10  SamplingPolicies ────────────────────────────────────────────────────
DECLARE @PCapOne5 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');

INSERT INTO dbo.SamplingPolicies
    (Name, Description, ProjectId, CallTypeFilter, SamplingMethod, SampleValue,
     IsActive, CreatedBy, CreatedAt, UpdatedAt)
VALUES
    ('10% Random Sampling',
     'Sample 10% of all completed evaluations for human review.',
     @PCapOne5, NULL, 'Percentage', 10, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00'),

    ('Compliance Risk — All Failing Calls',
     '100% review for calls with compliance-related failures.',
     @PCapOne5, 'Compliance', 'Percentage', 100, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00'),

    ('New Agents — 5 Calls per Week',
     'Review the first 5 calls per week for newly onboarded agents.',
     @PCapOne5, NULL, 'Count', 5, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00');
GO

-- ── 5.11  KnowledgeSources & Documents ───────────────────────────────────────
DECLARE @PCapOne6 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');

INSERT INTO dbo.KnowledgeSources (Name, ConnectorType, Description, IsActive, ProjectId, CreatedAt)
VALUES
    ('Capital One QA Policy Documents', 'ManualUpload',
     'Internal QA policies, compliance guidelines, and evaluation rubrics for Capital One customer support',
     1, @PCapOne6, '2025-01-01 00:00:00');
GO

DECLARE @KS1 INT = (SELECT Id FROM dbo.KnowledgeSources WHERE Name = 'Capital One QA Policy Documents');

INSERT INTO dbo.KnowledgeDocuments (SourceId, Title, FileName, Content, Tags, ContentSizeBytes, UploadedAt)
VALUES
    (@KS1, 'PCI DSS Agent Guidelines v4.0', 'PCI_DSS_Agent_Guidelines_v4.pdf',
     'PCI DSS Scope for Call Centre Agents. '
     + 'Agents MUST NOT: Ask customers to read aloud full card numbers; Write down, store, or repeat Primary Account Numbers (PAN); Record sensitive authentication data after authorisation. '
     + 'Agents MUST: Complete CID verification before accessing any account information; Use masked card numbers (last 4 digits only) when confirming identity; '
     + 'Immediately terminate calls where customers attempt to provide full card numbers verbally. '
     + 'Violations are classified as Critical and result in immediate remediation and mandatory retraining.',
     'PCI,Compliance,Security,CID', 1240, '2025-01-01 00:00:00'),

    (@KS1, 'Reg Z Required Disclosures Script', 'RegZ_Disclosure_Script_2025.pdf',
     'Regulation Z — Required Verbal Disclosures for Credit Card Calls. '
     + 'When discussing APR or fees, agents must deliver the following scripted disclosure: '
     + '"Just to let you know, [Product Name] has a variable APR of [X]% for purchases, [Y]% for cash advances, and a minimum interest charge of $[Z]. '
     + 'Late fees are up to $[amount]. Please refer to your Cardmember Agreement for full terms." '
     + 'This disclosure is mandatory whenever opening a new account, discussing promotional rates, responding to balance transfer enquiries, or any conversation involving credit terms or fees. '
     + 'Failure to deliver this disclosure in full is a Reg Z compliance violation.',
     'Compliance,RegZ,Disclosures,Fees', 980, '2025-01-01 00:00:00'),

    (@KS1, 'QA Evaluation Rubric — Communication Skills', 'QA_Rubric_Communication_2025.pdf',
     'Communication Skills Evaluation Rubric. '
     + 'Score 5 (Outstanding): Agent communicates with exceptional clarity, warmth, and professionalism. '
     + 'Score 4 (Exceeds Standard): Clear and professional communication with minor inconsistencies. Active listening demonstrated. '
     + 'Score 3 (Meets Standard): Acceptable communication. Occasional lapses but no significant impact on customer experience. '
     + 'Score 2 (Needs Improvement): Unclear communication, interrupts customer, or uses inappropriate tone. '
     + 'Score 1 (Unacceptable): Rude, dismissive, or seriously unclear communication. Immediate coaching required.',
     'Communication,Rubric,Scoring,Training', 870, '2025-01-15 00:00:00');
GO

-- ── 5.12  Evaluation Results & Scores (sample Capital One audits) ─────────────
DECLARE @FormCO2 INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Capital One — Credit Card Customer Support QA Form');

-- Result 1 — Sarah Mitchell (high performing)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate, Notes)
VALUES
    (@FormCO2, 'admin', '2025-01-15 09:00:00', 'Sarah Mitchell', 'COF-2025-00142', '2025-01-14 00:00:00',
     'Excellent across all areas. Strong compliance adherence and outstanding customer rapport.');

DECLARE @R1 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R1, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Professional Greeting',          4),
    ('Customer Identity Verification', 1),
    ('Brand Introduction',             4),
    ('First Call Resolution',          5),
    ('Product & Policy Knowledge',     4),
    ('Information Accuracy',           5),
    ('Problem-Solving Ability',        4),
    ('Verbal Clarity & Articulation',  5),
    ('Active Listening',               4),
    ('Empathy & Rapport Building',     5),
    ('Pace, Tone & Energy',            4),
    ('CFPB Regulatory Compliance',     1),
    ('Required Disclosures',           1),
    ('PCI Data Security',              1),
    ('Issue Summary & Confirmation',   5),
    ('Offer of Further Assistance',    5),
    ('Professional Sign-Off',          4)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCO2;

-- Result 2 — James Kowalski (missed disclosure)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate, Notes)
VALUES
    (@FormCO2, 'admin', '2025-01-22 10:00:00', 'James Kowalski', 'COF-2025-00215', '2025-01-21 00:00:00',
     'Good performance overall. Missed required APR disclosure on the first attempt — corrected after customer prompt.');

DECLARE @R2 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R2, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Professional Greeting',          4),
    ('Customer Identity Verification', 1),
    ('Brand Introduction',             3),
    ('First Call Resolution',          4),
    ('Product & Policy Knowledge',     3),
    ('Information Accuracy',           4),
    ('Problem-Solving Ability',        3),
    ('Verbal Clarity & Articulation',  4),
    ('Active Listening',               3),
    ('Empathy & Rapport Building',     3),
    ('Pace, Tone & Energy',            3),
    ('CFPB Regulatory Compliance',     1),
    ('Required Disclosures',           0),
    ('PCI Data Security',              1),
    ('Issue Summary & Confirmation',   3),
    ('Offer of Further Assistance',    3),
    ('Professional Sign-Off',          4)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCO2;

-- Result 3 — Priya Nair (outstanding)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate, Notes)
VALUES
    (@FormCO2, 'admin', '2025-02-05 11:00:00', 'Priya Nair', 'COF-2025-00318', '2025-02-04 00:00:00',
     'Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout.');

DECLARE @R3 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R3, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Professional Greeting',          5),
    ('Customer Identity Verification', 1),
    ('Brand Introduction',             5),
    ('First Call Resolution',          5),
    ('Product & Policy Knowledge',     5),
    ('Information Accuracy',           5),
    ('Problem-Solving Ability',        5),
    ('Verbal Clarity & Articulation',  5),
    ('Active Listening',               5),
    ('Empathy & Rapport Building',     5),
    ('Pace, Tone & Energy',            5),
    ('CFPB Regulatory Compliance',     1),
    ('Required Disclosures',           1),
    ('PCI Data Security',              1),
    ('Issue Summary & Confirmation',   5),
    ('Offer of Further Assistance',    5),
    ('Professional Sign-Off',          5)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCO2;

-- Result 4 — Derek Thompson (PCI violation, below standard)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate, Notes)
VALUES
    (@FormCO2, 'admin', '2025-02-12 12:00:00', 'Derek Thompson', 'COF-2025-00401', '2025-02-11 00:00:00',
     'Below standard. Failed to complete full CID verification and asked customer to repeat card number aloud — PCI violation. Immediate coaching required.');

DECLARE @R4 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R4, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Professional Greeting',          3),
    ('Customer Identity Verification', 0),
    ('Brand Introduction',             2),
    ('First Call Resolution',          2),
    ('Product & Policy Knowledge',     2),
    ('Information Accuracy',           3),
    ('Problem-Solving Ability',        2),
    ('Verbal Clarity & Articulation',  3),
    ('Active Listening',               2),
    ('Empathy & Rapport Building',     2),
    ('Pace, Tone & Energy',            2),
    ('CFPB Regulatory Compliance',     1),
    ('Required Disclosures',           1),
    ('PCI Data Security',              0),
    ('Issue Summary & Confirmation',   2),
    ('Offer of Further Assistance',    2),
    ('Professional Sign-Off',          3)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCO2;

-- Result 5 — Maria Gonzalez (solid)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate, Notes)
VALUES
    (@FormCO2, 'admin', '2025-02-20 13:00:00', 'Maria Gonzalez', 'COF-2025-00487', '2025-02-19 00:00:00',
     'Solid performance with good first-call resolution. Slight hesitation on product knowledge for balance transfer promotions.');

DECLARE @R5 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R5, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Professional Greeting',          5),
    ('Customer Identity Verification', 1),
    ('Brand Introduction',             4),
    ('First Call Resolution',          4),
    ('Product & Policy Knowledge',     3),
    ('Information Accuracy',           4),
    ('Problem-Solving Ability',        4),
    ('Verbal Clarity & Articulation',  4),
    ('Active Listening',               4),
    ('Empathy & Rapport Building',     4),
    ('Pace, Tone & Energy',            4),
    ('CFPB Regulatory Compliance',     1),
    ('Required Disclosures',           1),
    ('PCI Data Security',              1),
    ('Issue Summary & Confirmation',   4),
    ('Offer of Further Assistance',    4),
    ('Professional Sign-Off',          5)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCO2;
GO

-- ── 5.13  HumanReviewItems ────────────────────────────────────────────────────
DECLARE @SP1 INT = (SELECT Id FROM dbo.SamplingPolicies WHERE Name = '10% Random Sampling');
DECLARE @R1x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Sarah Mitchell' ORDER BY EvaluatedAt);
DECLARE @R2x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'James Kowalski' ORDER BY EvaluatedAt);
DECLARE @R4x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Derek Thompson' ORDER BY EvaluatedAt);

INSERT INTO dbo.HumanReviewItems
    (EvaluationResultId, SamplingPolicyId, SampledAt, SampledBy, AssignedTo, Status,
     ReviewerComment, ReviewVerdict, ReviewedBy, ReviewedAt)
VALUES
    (@R1x, @SP1, '2025-01-15 10:00:00', 'system', 'admin', 'Reviewed',
     'Agree with the AI scoring. Agent demonstrated excellent product knowledge and compliance.',
     'Agree', 'admin', '2025-01-16 09:00:00'),

    (@R2x, @SP1, '2025-01-22 11:00:00', 'system', 'admin', 'Reviewed',
     'Partially agree — the Required Disclosures failure was borderline; agent did mention APR but not in the required format.',
     'Partial', 'admin', '2025-01-23 10:00:00'),

    (@R4x, @SP1, '2025-02-12 14:00:00', 'system', 'admin', 'Pending',
     NULL, NULL, NULL, NULL);
GO

-- ── 5.14  CallPipeline ────────────────────────────────────────────────────────
DECLARE @FormCO3  INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Capital One — Credit Card Customer Support QA Form');
DECLARE @PCapOne7 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @R1p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Sarah Mitchell' ORDER BY EvaluatedAt);
DECLARE @R2p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'James Kowalski' ORDER BY EvaluatedAt);
DECLARE @R3p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Priya Nair'     ORDER BY EvaluatedAt);
DECLARE @R4p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Derek Thompson' ORDER BY EvaluatedAt);

INSERT INTO dbo.CallPipelineJobs
    (Name, SourceType, FormId, ProjectId, Status, CreatedAt, StartedAt, CompletedAt, CreatedBy)
VALUES
    ('Capital One — Weekly Batch Jan W3 2025', 'BatchUrl', @FormCO3, @PCapOne7, 'Completed',
     '2025-01-20 00:00:00', '2025-01-20 09:00:00', '2025-01-20 09:18:00', 'admin');

DECLARE @Job1 INT = SCOPE_IDENTITY();

INSERT INTO dbo.CallPipelineItems
    (JobId, SourceReference, AgentName, CallReference, CallDate, Status,
     CreatedAt, ProcessedAt, EvaluationResultId, ScorePercent, AiReasoning)
VALUES
    (@Job1, 'https://recordings.capitalone.internal/calls/COF-2025-00142.mp3',
     'Sarah Mitchell', 'COF-2025-00142', '2025-01-14 00:00:00', 'Completed',
     '2025-01-20 00:00:00', '2025-01-20 09:05:00', @R1p, 91.2,
     'Excellent performance across all evaluation areas. Strong compliance adherence and outstanding customer rapport.'),
    (@Job1, 'https://recordings.capitalone.internal/calls/COF-2025-00215.mp3',
     'James Kowalski', 'COF-2025-00215', '2025-01-21 00:00:00', 'Completed',
     '2025-01-20 00:00:00', '2025-01-20 09:10:00', @R2p, 73.5,
     'Good overall performance. Missed required APR disclosure on the first attempt — corrected after customer prompt.'),
    (@Job1, 'https://recordings.capitalone.internal/calls/COF-2025-00155.mp3',
     'Maria Gonzalez', 'COF-2025-00155', '2025-01-16 00:00:00', 'Failed',
     '2025-01-20 00:00:00', '2025-01-20 09:15:00', NULL, NULL, NULL);

UPDATE dbo.CallPipelineItems
SET ErrorMessage = 'Audio quality too poor to transcribe reliably (SNR < 10 dB).'
WHERE JobId = @Job1 AND AgentName = 'Maria Gonzalez';

INSERT INTO dbo.CallPipelineJobs
    (Name, SourceType, FormId, ProjectId, Status, CreatedAt, StartedAt, CompletedAt, CreatedBy)
VALUES
    ('Capital One — Weekly Batch Feb W1 2025', 'BatchUrl', @FormCO3, @PCapOne7, 'Completed',
     '2025-02-03 00:00:00', '2025-02-03 09:00:00', '2025-02-03 09:22:00', 'admin');

DECLARE @Job2 INT = SCOPE_IDENTITY();

INSERT INTO dbo.CallPipelineItems
    (JobId, SourceReference, AgentName, CallReference, CallDate, Status,
     CreatedAt, ProcessedAt, EvaluationResultId, ScorePercent, AiReasoning)
VALUES
    (@Job2, 'https://recordings.capitalone.internal/calls/COF-2025-00318.mp3',
     'Priya Nair', 'COF-2025-00318', '2025-02-04 00:00:00', 'Completed',
     '2025-02-03 00:00:00', '2025-02-03 09:08:00', @R3p, 98.5,
     'Outstanding call. Customer escalation handled with exceptional empathy. Full compliance adherence throughout.'),
    (@Job2, 'https://recordings.capitalone.internal/calls/COF-2025-00401.mp3',
     'Derek Thompson', 'COF-2025-00401', '2025-02-11 00:00:00', 'Completed',
     '2025-02-03 00:00:00', '2025-02-03 09:16:00', @R4p, 38.2,
     'Below standard. Failed CID verification and PCI DSS violation detected. Immediate coaching required.');
GO

-- ── 5.15  Training Plans ──────────────────────────────────────────────────────
DECLARE @PCapOne8 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');
DECLARE @R4t INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Derek Thompson' ORDER BY EvaluatedAt);
DECLARE @R2t INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'James Kowalski' ORDER BY EvaluatedAt);

INSERT INTO dbo.TrainingPlans
    (Title, Description, AgentName, AgentUsername, TrainerName, TrainerUsername,
     Status, DueDate, ProjectId, EvaluationResultId, HumanReviewItemId,
     CreatedBy, CreatedAt, UpdatedAt)
VALUES
    ('PCI DSS Compliance Remediation — Derek Thompson',
     'Immediate coaching required following PCI DSS violation (COF-2025-00401). Agent asked customer to repeat card number verbally and failed full CID verification.',
     'Derek Thompson', 'agent1', 'admin', NULL,
     'Active', '2025-03-10 00:00:00',
     @PCapOne8, @R4t, NULL,
     'admin', '2025-02-12 00:00:00', '2025-02-12 00:00:00');

DECLARE @TP1 INT = SCOPE_IDENTITY();

INSERT INTO dbo.TrainingPlanItems
    (TrainingPlanId, TargetArea, ItemType, Content, Status, [Order])
VALUES
    (@TP1, 'Compliance & Procedures', 'Observation',
     'Agent asked the customer to repeat their full card number aloud, violating PCI DSS data security requirements.',
     'Pending', 0),
    (@TP1, 'Compliance & Procedures', 'Observation',
     'Customer Identity Verification (CID) process was not completed before account details were discussed.',
     'Pending', 1),
    (@TP1, 'Compliance & Procedures', 'Recommendation',
     'Complete the PCI DSS e-learning module (30 min) and pass the assessment with a minimum score of 80%.',
     'Pending', 2),
    (@TP1, 'Compliance & Procedures', 'Recommendation',
     'Complete a live role-play session with the trainer covering CID verification and sensitive data handling.',
     'Pending', 3),
    (@TP1, 'Call Opening', 'Recommendation',
     'Review the CID verification checklist and practise the verification script until it becomes second nature.',
     'Pending', 4);

INSERT INTO dbo.TrainingPlans
    (Title, Description, AgentName, AgentUsername, TrainerName, TrainerUsername,
     Status, DueDate, ProjectId, EvaluationResultId, HumanReviewItemId,
     CreatedBy, CreatedAt, UpdatedAt)
VALUES
    ('Required Disclosures Coaching — James Kowalski',
     'APR and fee disclosure was not provided in the required format on call COF-2025-00215. Coaching to reinforce Reg Z disclosure requirements.',
     'James Kowalski', NULL, 'admin', NULL,
     'InProgress', '2025-03-05 00:00:00',
     @PCapOne8, @R2t, NULL,
     'admin', '2025-01-22 00:00:00', '2025-02-01 00:00:00');

DECLARE @TP2 INT = SCOPE_IDENTITY();

INSERT INTO dbo.TrainingPlanItems
    (TrainingPlanId, TargetArea, ItemType, Content, Status, [Order], CompletedBy, CompletedAt, CompletionNotes)
VALUES
    (@TP2, 'Compliance & Procedures', 'Observation',
     'Required APR and fee disclosures were mentioned but not delivered in the mandatory scripted format as required by Reg Z.',
     'Done', 0, 'admin', '2025-02-01 00:00:00', 'Agent reviewed the Reg Z disclosure script and confirmed understanding.'),
    (@TP2, 'Compliance & Procedures', 'Recommendation',
     'Review the Reg Z required disclosure scripts for all Capital One credit card product categories.',
     'InProgress', 1, NULL, NULL, NULL),
    (@TP2, 'Compliance & Procedures', 'Recommendation',
     'Conduct two supervised calls where the trainer monitors disclosure delivery in real time.',
     'Pending', 2, NULL, NULL, NULL);
GO

-- ── 5.16  Audit Log entries (sample PII and API call events) ─────────────────
DECLARE @PCapOne9 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Capital One');

INSERT INTO dbo.AuditLogs
    (ProjectId, Category, EventType, Outcome, Actor, PiiTypesDetected,
     HttpMethod, Endpoint, HttpStatusCode, DurationMs, Provider, Details, OccurredAt)
VALUES
    (@PCapOne9, 'PiiEvent', 'PiiDetected', 'Detected', 'auto-audit', 'EMAIL,PHONE',
     NULL, NULL, NULL, NULL, NULL,
     'PII detected in transcript for COF-2025-00215 before LLM call.',
     '2025-01-22 10:05:00'),

    (@PCapOne9, 'PiiEvent', 'PiiRedacted', 'Redacted', 'auto-audit', 'EMAIL,PHONE',
     NULL, NULL, NULL, NULL, NULL,
     'PII tokens replaced with [EMAIL] and [PHONE] before LLM call. COF-2025-00215.',
     '2025-01-22 10:05:01'),

    (@PCapOne9, 'ExternalApiCall', 'LlmAudit', 'Success', 'auto-audit', NULL,
     'POST', 'https://my-openai.openai.azure.com/openai/deployments/gpt-4o/chat/completions', 200, 3420, 'AzureOpenAI',
     'Job: Capital One — Weekly Batch Jan W3 2025; Ref: COF-2025-00142',
     '2025-01-20 09:05:00'),

    (@PCapOne9, 'ExternalApiCall', 'LlmAudit', 'Success', 'auto-audit', NULL,
     'POST', 'https://my-openai.openai.azure.com/openai/deployments/gpt-4o/chat/completions', 200, 4110, 'AzureOpenAI',
     'Job: Capital One — Weekly Batch Jan W3 2025; Ref: COF-2025-00215',
     '2025-01-20 09:10:00'),

    (@PCapOne9, 'ExternalApiCall', 'UrlFetch', 'Failure', 'pipeline:Capital One — Weekly Batch Jan W3 2025', NULL,
     'GET', 'https://recordings.capitalone.internal/calls/COF-2025-00155.mp3', NULL, 0, NULL,
     'Audio quality too poor to transcribe reliably (SNR < 10 dB).',
     '2025-01-20 09:15:00');
GO

-- =============================================================================
-- 6. VERIFICATION QUERIES (comment out if not needed)
-- =============================================================================

SELECT 'Tables created:' AS [Info],
       COUNT(*) AS TableCount
FROM   INFORMATION_SCHEMA.TABLES
WHERE  TABLE_TYPE = 'BASE TABLE' AND TABLE_SCHEMA = 'dbo';

SELECT 'AppUsers'         AS [Table], COUNT(*) AS Rows FROM dbo.AppUsers         UNION ALL
SELECT 'Projects',                    COUNT(*)          FROM dbo.Projects         UNION ALL
SELECT 'Lobs',                        COUNT(*)          FROM dbo.Lobs             UNION ALL
SELECT 'EvaluationForms',             COUNT(*)          FROM dbo.EvaluationForms  UNION ALL
SELECT 'FormSections',                COUNT(*)          FROM dbo.FormSections     UNION ALL
SELECT 'FormFields',                  COUNT(*)          FROM dbo.FormFields       UNION ALL
SELECT 'Parameters',                  COUNT(*)          FROM dbo.Parameters       UNION ALL
SELECT 'RatingCriteria',              COUNT(*)          FROM dbo.RatingCriteria   UNION ALL
SELECT 'RatingLevels',                COUNT(*)          FROM dbo.RatingLevels     UNION ALL
SELECT 'ParameterClubs',              COUNT(*)          FROM dbo.ParameterClubs   UNION ALL
SELECT 'ParameterClubItems',          COUNT(*)          FROM dbo.ParameterClubItems UNION ALL
SELECT 'KnowledgeSources',            COUNT(*)          FROM dbo.KnowledgeSources UNION ALL
SELECT 'KnowledgeDocuments',          COUNT(*)          FROM dbo.KnowledgeDocuments UNION ALL
SELECT 'SamplingPolicies',            COUNT(*)          FROM dbo.SamplingPolicies UNION ALL
SELECT 'EvaluationResults',           COUNT(*)          FROM dbo.EvaluationResults UNION ALL
SELECT 'EvaluationScores',            COUNT(*)          FROM dbo.EvaluationScores UNION ALL
SELECT 'CallPipelineJobs',            COUNT(*)          FROM dbo.CallPipelineJobs UNION ALL
SELECT 'CallPipelineItems',           COUNT(*)          FROM dbo.CallPipelineItems UNION ALL
SELECT 'HumanReviewItems',            COUNT(*)          FROM dbo.HumanReviewItems UNION ALL
SELECT 'TrainingPlans',               COUNT(*)          FROM dbo.TrainingPlans    UNION ALL
SELECT 'TrainingPlanItems',           COUNT(*)          FROM dbo.TrainingPlanItems UNION ALL
SELECT 'AuditLogs',                   COUNT(*)          FROM dbo.AuditLogs;
GO

PRINT '====================================================';
PRINT 'QA Automation Platform — database setup complete.';
PRINT '====================================================';
GO
