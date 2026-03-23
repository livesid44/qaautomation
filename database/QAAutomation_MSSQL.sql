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
    CONSTRAINT FK_TP_HumanReview     FOREIGN KEY (HumanReviewItemId)  REFERENCES dbo.HumanReviewItems (Id) ON DELETE SET NULL
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
-- Passwords are BCrypt hashes.
-- admin     → password: Admin@123
-- qamanager → password: QA@Manager1
-- analyst1  → password: Analyst@1
-- trainer1  → password: Trainer@1
-- agent1    → password: Agent@1
INSERT INTO dbo.AppUsers (Username, PasswordHash, Email, Role, IsActive, CreatedAt)
VALUES
    ('admin',
     '$2a$11$rVHpJzfxbXpRKpUqmMoJ8u7YkqA5k7qW3xN2vL0cGzHlP4sJ8cH6i',
     'admin@qaplatform.local', 'Admin', 1, '2026-01-01 00:00:00'),
    ('qamanager',
     '$2a$11$mK3gLpQz9wX1YcN5sR7AiOeJp8fKl4dV6hMtW2oBnC0rUs6FDyqPm',
     'qamanager@qaplatform.local', 'Manager', 1, '2026-01-01 00:00:00'),
    ('analyst1',
     '$2a$11$bX7nYp4wE2QkRf9mZ3sCuuL5JhD8GiV0tFxM6oKjPN1rWA2eBdqIe',
     'analyst1@qaplatform.local', 'Analyst', 1, '2026-01-02 00:00:00'),
    ('trainer1',
     '$2a$11$cM8oZq5xF3RlSg0nA4tDvvM6KiE9HjW1uGyN7pLkQO2sSB3fCerJf',
     'trainer1@qaplatform.local', 'User', 1, '2026-01-02 00:00:00'),
    ('agent1',
     '$2a$11$dN9pAr6yG4SmTh1oB5uEwwN7LjF0IkX2vHzO8qMlRP3tTC4gDfsKg',
     'agent1@qaplatform.local', 'User', 1, '2026-01-02 00:00:00');
GO

-- ── 5.3  Projects ────────────────────────────────────────────────────────────
INSERT INTO dbo.Projects (Name, Description, IsActive, PiiProtectionEnabled, PiiRedactionMode, CreatedAt)
VALUES
    ('Customer Service Excellence',
     'End-to-end QA for the customer service call centre covering all inbound queues.',
     1, 1, 'Redact', '2026-01-05 00:00:00'),
    ('Sales & Upsell',
     'QA program for the outbound sales and upsell team.',
     1, 0, 'Redact', '2026-01-05 00:00:00'),
    ('Technical Support',
     'Level 1 and Level 2 technical support QA monitoring.',
     1, 1, 'Block',  '2026-01-06 00:00:00');
GO

-- ── 5.4  UserProjectAccesses ──────────────────────────────────────────────────
-- qamanager and analyst1 get access to all three projects
-- trainer1 and agent1 only access Project 1 (Customer Service)
INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, p.Id, '2026-01-06 00:00:00'
FROM   dbo.AppUsers u
CROSS JOIN dbo.Projects p
WHERE  u.Username IN ('qamanager','analyst1');

INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, p.Id, '2026-01-06 00:00:00'
FROM   dbo.AppUsers u
JOIN   dbo.Projects p ON p.Name = 'Customer Service Excellence'
WHERE  u.Username IN ('trainer1','agent1');
GO

-- ── 5.5  LOBs ────────────────────────────────────────────────────────────────
DECLARE @P1 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');
DECLARE @P2 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Sales & Upsell');
DECLARE @P3 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Technical Support');

INSERT INTO dbo.Lobs (ProjectId, Name, Description, IsActive, CreatedAt)
VALUES
    (@P1, 'Inbound General',    'General inbound customer enquiries.',          1, '2026-01-10 00:00:00'),
    (@P1, 'Complaints',         'Escalated complaint calls.',                   1, '2026-01-10 00:00:00'),
    (@P1, 'Retention',          'Customer retention and churn prevention.',     1, '2026-01-10 00:00:00'),
    (@P2, 'Cold Outbound',      'Cold outbound sales calls.',                   1, '2026-01-10 00:00:00'),
    (@P2, 'Warm Leads',         'Calls to pre-qualified warm leads.',           1, '2026-01-10 00:00:00'),
    (@P3, 'Level 1 Support',    'First-contact technical resolution.',          1, '2026-01-10 00:00:00'),
    (@P3, 'Level 2 Escalations','Complex technical escalations.',               1, '2026-01-10 00:00:00');
GO

-- ── 5.6  Parameters ──────────────────────────────────────────────────────────
-- Global (NULL ProjectId) parameters reusable across all projects
INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
VALUES
    ('Opening & Greeting',
     'Did the agent greet the customer professionally, state their name and the company name?',
     'Communication', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Active Listening',
     'Did the agent demonstrate active listening — paraphrase, confirm understanding, avoid interruptions?',
     'Communication', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Empathy & Tone',
     'Did the agent show empathy, maintain a warm and professional tone throughout the call?',
     'Communication', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Problem Identification',
     'Did the agent accurately identify the root cause of the customer issue?',
     'Resolution', 1.5, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Resolution & Accuracy',
     'Was the resolution provided correct, complete, and within policy?',
     'Resolution', 2.0, 1, 'KnowledgeBased', NULL, '2026-01-08 00:00:00'),

    ('Hold Procedure',
     'Did the agent follow the hold procedure — ask permission, provide reason, check back every 2 minutes?',
     'Process', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Data Verification',
     'Did the agent verify at least two customer identifiers before discussing account details?',
     'Compliance', 2.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Regulatory Compliance',
     'Were all mandatory disclosures and regulatory scripts read verbatim where required?',
     'Compliance', 3.0, 1, 'KnowledgeBased', NULL, '2026-01-08 00:00:00'),

    ('Closing & Wrap-up',
     'Did the agent summarise actions, confirm customer satisfaction and close the call properly?',
     'Communication', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('After-Call Work',
     'Was the call correctly disposed, notes added, and any follow-up tasks created?',
     'Process', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Upsell Attempt',
     'Did the agent identify a natural upsell opportunity and present it compliantly?',
     'Sales', 1.5, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Objection Handling',
     'Did the agent handle customer objections confidently and logically without applying pressure?',
     'Sales', 1.5, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Technical Accuracy',
     'Was the technical information or troubleshooting step provided correct for the described issue?',
     'Technical', 2.0, 1, 'KnowledgeBased', NULL, '2026-01-08 00:00:00'),

    ('Escalation Handling',
     'When escalation was required, was the process followed correctly and communicated clearly?',
     'Process', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00'),

    ('Call Control',
     'Did the agent manage the pace, direction and duration of the call effectively?',
     'Communication', 1.0, 1, 'LLM', NULL, '2026-01-08 00:00:00');
GO

-- ── 5.7  RatingCriteria & Levels ────────────────────────────────────────────
INSERT INTO dbo.RatingCriteria (Name, Description, MinScore, MaxScore, IsActive, ProjectId, CreatedAt)
VALUES
    ('5-Point Excellence Scale',
     'Standard 1–5 quality rating where 5 = Exceptional and 1 = Unacceptable.',
     1, 5, 1, NULL, '2026-01-08 00:00:00'),

    ('Yes / No / Partial',
     'Binary or partial compliance check.',
     0, 2, 1, NULL, '2026-01-08 00:00:00'),

    ('Compliance Gate',
     'Mandatory compliance check — fail causes overall call to be non-compliant.',
     0, 1, 1, NULL, '2026-01-08 00:00:00');
GO

DECLARE @RC1 INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = '5-Point Excellence Scale');
DECLARE @RC2 INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'Yes / No / Partial');
DECLARE @RC3 INT = (SELECT Id FROM dbo.RatingCriteria WHERE Name = 'Compliance Gate');

INSERT INTO dbo.RatingLevels (CriteriaId, Score, Label, Description, Color)
VALUES
    (@RC1, 5, 'Exceptional', 'Far exceeds expectations; customer delight achieved.', '#198754'),
    (@RC1, 4, 'Good',        'Meets expectations with minor improvement areas.',     '#0d6efd'),
    (@RC1, 3, 'Satisfactory','Partially meets expectations; coaching required.',     '#ffc107'),
    (@RC1, 2, 'Needs Improvement','Significant gaps; structured development needed.','#fd7e14'),
    (@RC1, 1, 'Unacceptable','Critical failure; immediate remediation required.',    '#dc3545'),

    (@RC2, 2, 'Yes',     'Fully met.',         '#198754'),
    (@RC2, 1, 'Partial', 'Partially met.',     '#ffc107'),
    (@RC2, 0, 'No',      'Not met.',           '#dc3545'),

    (@RC3, 1, 'Pass',    'Compliant.',         '#198754'),
    (@RC3, 0, 'Fail',    'Non-compliant.',     '#dc3545');
GO

-- ── 5.8  ParameterClubs ──────────────────────────────────────────────────────
DECLARE @P1b INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');

INSERT INTO dbo.ParameterClubs (Name, Description, IsActive, ProjectId, CreatedAt)
VALUES
    ('Core Communication Club',    'Covers all communication-related parameters.',     1, @P1b, '2026-01-09 00:00:00'),
    ('Compliance & Process Club',  'Mandatory compliance and process parameters.',     1, @P1b, '2026-01-09 00:00:00'),
    ('Sales Effectiveness Club',   'Sales-specific parameters for upsell quality.',   1, NULL,  '2026-01-09 00:00:00');
GO

DECLARE @Club1 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Core Communication Club');
DECLARE @Club2 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Compliance & Process Club');
DECLARE @Club3 INT = (SELECT Id FROM dbo.ParameterClubs WHERE Name = 'Sales Effectiveness Club');

INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride)
SELECT @Club1, Id, ROW_NUMBER() OVER (ORDER BY Id), NULL
FROM   dbo.Parameters WHERE Name IN ('Opening & Greeting','Active Listening','Empathy & Tone','Closing & Wrap-up','Call Control');

INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride)
SELECT @Club2, Id, ROW_NUMBER() OVER (ORDER BY Id), NULL
FROM   dbo.Parameters WHERE Name IN ('Data Verification','Regulatory Compliance','Hold Procedure','After-Call Work');

INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride)
SELECT @Club3, Id, ROW_NUMBER() OVER (ORDER BY Id), 2.0
FROM   dbo.Parameters WHERE Name IN ('Upsell Attempt','Objection Handling','Closing & Wrap-up');
GO

-- ── 5.9  Evaluation Forms ─────────────────────────────────────────────────────
DECLARE @LobInbound  INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'Inbound General');
DECLARE @LobCompl    INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'Complaints');
DECLARE @LobSalesCold INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'Cold Outbound');
DECLARE @LobL1       INT = (SELECT Id FROM dbo.Lobs WHERE Name = 'Level 1 Support');

INSERT INTO dbo.EvaluationForms (Name, Description, IsActive, LobId, CreatedAt, UpdatedAt)
VALUES
    ('Inbound Customer Service QA Form',
     'Standard QA scorecard for all inbound customer service calls.',
     1, @LobInbound, '2026-01-12 00:00:00', '2026-01-12 00:00:00'),

    ('Complaints Handling Scorecard',
     'Enhanced form for escalated complaint calls with compliance gates.',
     1, @LobCompl, '2026-01-12 00:00:00', '2026-01-12 00:00:00'),

    ('Sales Call QA Form',
     'Outbound sales and upsell quality scorecard.',
     1, @LobSalesCold, '2026-01-12 00:00:00', '2026-01-12 00:00:00'),

    ('Tech Support Level 1 Form',
     'First-contact resolution QA form for L1 technical support.',
     1, @LobL1, '2026-01-12 00:00:00', '2026-01-12 00:00:00');
GO

-- ── 5.9a  Form Sections & Fields — Form 1: Inbound Customer Service ──────────
DECLARE @Form1 INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Inbound Customer Service QA Form');
DECLARE @Sec1  INT, @Sec2 INT, @Sec3 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@Form1, 'Opening & Communication', 'How the agent started and managed the conversation.', 1),
    (@Form1, 'Resolution Quality',      'How well the agent identified and resolved the issue.', 2),
    (@Form1, 'Compliance & Process',    'Mandatory compliance checks and process adherence.', 3);

SET @Sec1 = SCOPE_IDENTITY() - 2;
SET @Sec2 = @Sec1 + 1;
SET @Sec3 = @Sec1 + 2;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
VALUES
    -- Section 1 — Communication (FieldType=2 → Rating)
    (@Sec1, 'Opening & Greeting',  2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Opening & Greeting')),
    (@Sec1, 'Active Listening',    2, 1, 2, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Active Listening')),
    (@Sec1, 'Empathy & Tone',      2, 1, 3, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Empathy & Tone')),
    (@Sec1, 'Call Control',        2, 0, 4, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Call Control')),
    -- Section 2 — Resolution
    (@Sec2, 'Problem Identification', 2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Problem Identification')),
    (@Sec2, 'Resolution & Accuracy',  2, 1, 2, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Resolution & Accuracy')),
    -- Section 3 — Compliance
    (@Sec3, 'Data Verification',       2, 1, 1, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Data Verification')),
    (@Sec3, 'Hold Procedure',          2, 0, 2, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Hold Procedure')),
    (@Sec3, 'Regulatory Compliance',   2, 1, 3, 1, (SELECT Description FROM dbo.Parameters WHERE Name='Regulatory Compliance')),
    (@Sec3, 'Closing & Wrap-up',       2, 1, 4, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Closing & Wrap-up')),
    (@Sec3, 'After-Call Work',         2, 0, 5, 5, (SELECT Description FROM dbo.Parameters WHERE Name='After-Call Work')),
    -- Notes — TextArea (FieldType=1)
    (@Sec3, 'Additional Notes', 1, 0, 6, 5, 'Any additional observations not captured above.');
GO

-- ── 5.9b  Form Sections & Fields — Form 2: Complaints Handling ───────────────
DECLARE @Form2 INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Complaints Handling Scorecard');
DECLARE @CSec1 INT, @CSec2 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@Form2, 'Empathy & De-escalation', 'Ability to defuse tension and build rapport.', 1),
    (@Form2, 'Compliance & Resolution', 'Mandatory compliance checks for complaint calls.', 2);

SET @CSec1 = SCOPE_IDENTITY() - 1;
SET @CSec2 = @CSec1 + 1;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
VALUES
    (@CSec1, 'Empathy & Tone',      2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Empathy & Tone')),
    (@CSec1, 'Active Listening',    2, 1, 2, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Active Listening')),
    (@CSec1, 'Opening & Greeting',  2, 1, 3, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Opening & Greeting')),
    (@CSec2, 'Data Verification',   2, 1, 1, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Data Verification')),
    (@CSec2, 'Regulatory Compliance',2,1, 2, 1, (SELECT Description FROM dbo.Parameters WHERE Name='Regulatory Compliance')),
    (@CSec2, 'Resolution & Accuracy',2,1, 3, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Resolution & Accuracy')),
    (@CSec2, 'Closing & Wrap-up',   2, 1, 4, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Closing & Wrap-up')),
    (@CSec2, 'Reviewer Notes',      1, 0, 5, 5, 'Additional complaint notes.');
GO

-- ── 5.9c  Form Sections & Fields — Form 3: Sales ─────────────────────────────
DECLARE @Form3 INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Sales Call QA Form');
DECLARE @SSec1 INT, @SSec2 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@Form3, 'Sales Quality',     'Core sales skill assessment.', 1),
    (@Form3, 'Compliance',        'Sales compliance requirements.', 2);

SET @SSec1 = SCOPE_IDENTITY() - 1;
SET @SSec2 = @SSec1 + 1;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
VALUES
    (@SSec1, 'Opening & Greeting',  2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Opening & Greeting')),
    (@SSec1, 'Upsell Attempt',      2, 1, 2, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Upsell Attempt')),
    (@SSec1, 'Objection Handling',  2, 1, 3, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Objection Handling')),
    (@SSec1, 'Closing & Wrap-up',   2, 1, 4, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Closing & Wrap-up')),
    (@SSec2, 'Regulatory Compliance',2,1, 1, 1, (SELECT Description FROM dbo.Parameters WHERE Name='Regulatory Compliance')),
    (@SSec2, 'Data Verification',   2, 1, 2, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Data Verification')),
    (@SSec2, 'Notes',               1, 0, 3, 5, 'Sales call notes.');
GO

-- ── 5.9d  Form Sections & Fields — Form 4: Tech Support ──────────────────────
DECLARE @Form4 INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Tech Support Level 1 Form');
DECLARE @TSec1 INT, @TSec2 INT;

INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
VALUES
    (@Form4, 'Communication',      'Communication quality on the support call.', 1),
    (@Form4, 'Technical Quality',  'Technical accuracy and escalation handling.', 2);

SET @TSec1 = SCOPE_IDENTITY() - 1;
SET @TSec2 = @TSec1 + 1;

INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
VALUES
    (@TSec1, 'Opening & Greeting', 2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Opening & Greeting')),
    (@TSec1, 'Active Listening',   2, 1, 2, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Active Listening')),
    (@TSec1, 'Call Control',       2, 0, 3, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Call Control')),
    (@TSec2, 'Technical Accuracy', 2, 1, 1, 5, (SELECT Description FROM dbo.Parameters WHERE Name='Technical Accuracy')),
    (@TSec2, 'Problem Identification',2,1, 2, 5,(SELECT Description FROM dbo.Parameters WHERE Name='Problem Identification')),
    (@TSec2, 'Escalation Handling',2, 0, 3, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Escalation Handling')),
    (@TSec2, 'Hold Procedure',     2, 0, 4, 2, (SELECT Description FROM dbo.Parameters WHERE Name='Hold Procedure')),
    (@TSec2, 'Technical Notes',    1, 0, 5, 5, 'Diagnostic steps tried and outcome.');
GO

-- ── 5.10  SamplingPolicies ────────────────────────────────────────────────────
DECLARE @P1c INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');
DECLARE @P2c INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Sales & Upsell');

INSERT INTO dbo.SamplingPolicies
    (Name, Description, ProjectId, CallTypeFilter, SamplingMethod, SampleValue,
     IsActive, CreatedBy, CreatedAt, UpdatedAt)
VALUES
    ('10% Random Sample — CS',
     'Randomly selects 10% of all completed customer service audits for human review.',
     @P1c, NULL, 'Percentage', 10, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00'),

    ('100% Complaints Review',
     'All complaint-call audits are flagged for human review.',
     @P1c, 'Complaints', 'Percentage', 100, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00'),

    ('5 Sales Calls / Day',
     'Caps human review at 5 sales call audits per day.',
     @P2c, NULL, 'Count', 5, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00'),

    ('Long-Call Deep Dive',
     'Reviews any call audited with a duration over 10 minutes.',
     @P1c, NULL, 'Percentage', 100, 1, 'admin', '2026-01-15 00:00:00', '2026-01-15 00:00:00');

UPDATE dbo.SamplingPolicies
SET MinDurationSeconds = 600
WHERE Name = 'Long-Call Deep Dive';
GO

-- ── 5.11  KnowledgeSources & Documents ───────────────────────────────────────
DECLARE @P1d INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');

INSERT INTO dbo.KnowledgeSources (Name, ConnectorType, Description, IsActive, ProjectId, CreatedAt)
VALUES
    ('CS Policy Manual', 'ManualUpload',
     'Internal customer service policy and procedure documents.',
     1, @P1d, '2026-01-08 00:00:00'),
    ('Compliance Scripts', 'ManualUpload',
     'Approved regulatory scripts that must be read verbatim.',
     1, @P1d, '2026-01-08 00:00:00');
GO

DECLARE @KS1 INT = (SELECT Id FROM dbo.KnowledgeSources WHERE Name = 'CS Policy Manual');
DECLARE @KS2 INT = (SELECT Id FROM dbo.KnowledgeSources WHERE Name = 'Compliance Scripts');

INSERT INTO dbo.KnowledgeDocuments (SourceId, Title, FileName, Content, Tags, ContentSizeBytes, UploadedAt)
VALUES
    (@KS1, 'Hold Procedure Policy', 'hold_procedure.txt',
     'When placing a customer on hold: (1) Always ask permission — "May I place you on hold for a moment?" '
     + '(2) Provide a reason for the hold. (3) Check back every 2 minutes. '
     + '(4) Thank the customer for their patience when returning. '
     + 'Maximum hold time: 5 minutes. If resolution requires longer, offer a callback.',
     'hold,process,procedure', 520, '2026-01-09 00:00:00'),

    (@KS1, 'Data Verification Standard', 'data_verification.txt',
     'Before discussing any account details, agents must verify a minimum of TWO of the following identifiers: '
     + '(a) Full name, (b) Date of birth, (c) Account number, (d) Registered email, (e) Security password/PIN. '
     + 'If the customer fails verification, escalate to the fraud team immediately — do NOT proceed.',
     'verification,compliance,security', 390, '2026-01-09 00:00:00'),

    (@KS2, 'Regulatory Disclosure Script — General',  'disclosure_general.txt',
     'REQUIRED VERBATIM SCRIPT: '
     + '"This call may be recorded for quality and training purposes. '
     + 'By continuing this call you consent to the recording. '
     + 'Our full privacy policy is available at [company website]."',
     'regulatory,compliance,script,disclosure', 280, '2026-01-09 00:00:00'),

    (@KS2, 'Payment Card Industry Script', 'pci_script.txt',
     'REQUIRED VERBATIM SCRIPT BEFORE TAKING PAYMENT DETAILS: '
     + '"For your security, please note that I will never ask you to share your full card number, CVV, '
     + 'or PIN over the phone. If you are asked for these, please end the call and call us back on our '
     + 'official number. Are you happy to proceed?"',
     'pci,payment,compliance,script', 350, '2026-01-09 00:00:00');
GO

-- ── 5.12  Evaluation Results & Scores (sample completed audits) ───────────────
DECLARE @FormCS INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Inbound Customer Service QA Form');
DECLARE @FormCC INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Complaints Handling Scorecard');

-- Result 1 — High-scoring inbound call
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate,
     CallDurationSeconds, OverallReasoning, Notes)
VALUES
    (@FormCS, 'auto-audit', '2026-02-10 09:15:00', 'Sarah Johnson', 'REF-20260210-001', '2026-02-10 09:00:00',
     480, 'Agent demonstrated excellent communication throughout. Greeted professionally, verified customer, resolved billing query accurately. Minor improvement on wrap-up.', NULL);

DECLARE @R1 INT = SCOPE_IDENTITY();

-- Insert scores for Result 1 (map to field labels dynamically)
INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R1, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Opening & Greeting', 5),
    ('Active Listening',   4),
    ('Empathy & Tone',     5),
    ('Call Control',       4),
    ('Problem Identification', 5),
    ('Resolution & Accuracy',  4),
    ('Data Verification',      2),
    ('Hold Procedure',         2),
    ('Regulatory Compliance',  1),
    ('Closing & Wrap-up',      3),
    ('After-Call Work',        4)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCS;

-- Result 2 — Average inbound call
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate,
     CallDurationSeconds, OverallReasoning, Notes)
VALUES
    (@FormCS, 'auto-audit', '2026-02-11 11:30:00', 'Mark Davis', 'REF-20260211-007', '2026-02-11 11:15:00',
     720, 'Agent showed empathy but failed data verification on first attempt and skipped the required disclosure script. Resolution was partially correct.', NULL);

DECLARE @R2 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R2, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Opening & Greeting', 3),
    ('Active Listening',   3),
    ('Empathy & Tone',     4),
    ('Call Control',       3),
    ('Problem Identification', 3),
    ('Resolution & Accuracy',  2),
    ('Data Verification',      1),
    ('Hold Procedure',         1),
    ('Regulatory Compliance',  0),
    ('Closing & Wrap-up',      3),
    ('After-Call Work',        3)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCS;

-- Result 3 — Complaint call (high performing)
INSERT INTO dbo.EvaluationResults
    (FormId, EvaluatedBy, EvaluatedAt, AgentName, CallReference, CallDate,
     CallDurationSeconds, OverallReasoning, Notes)
VALUES
    (@FormCC, 'auto-audit', '2026-02-12 14:00:00', 'Priya Sharma', 'REF-20260212-COMP-003', '2026-02-12 13:45:00',
     960, 'Exceptional complaint handling. Agent de-escalated quickly, verified customer properly, read disclosure correctly, and resolved the billing dispute with a fair outcome.', NULL);

DECLARE @R3 INT = SCOPE_IDENTITY();

INSERT INTO dbo.EvaluationScores (ResultId, FieldId, Value, NumericValue)
SELECT @R3, ff.Id, CAST(v.Score AS NVARCHAR(10)), v.Score
FROM (VALUES
    ('Empathy & Tone',       5),
    ('Active Listening',     5),
    ('Opening & Greeting',   4),
    ('Data Verification',    2),
    ('Regulatory Compliance',1),
    ('Resolution & Accuracy',5),
    ('Closing & Wrap-up',    5)
) AS v(Label, Score)
JOIN dbo.FormFields ff ON ff.Label = v.Label
JOIN dbo.FormSections fs ON fs.Id = ff.SectionId AND fs.FormId = @FormCC;
GO

-- ── 5.13  HumanReviewItems ────────────────────────────────────────────────────
DECLARE @SP1 INT = (SELECT Id FROM dbo.SamplingPolicies WHERE Name = '10% Random Sample — CS');
DECLARE @SP2 INT = (SELECT Id FROM dbo.SamplingPolicies WHERE Name = '100% Complaints Review');
DECLARE @R1x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Sarah Johnson' ORDER BY EvaluatedAt);
DECLARE @R2x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Mark Davis'   ORDER BY EvaluatedAt);
DECLARE @R3x INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Priya Sharma' ORDER BY EvaluatedAt);

INSERT INTO dbo.HumanReviewItems
    (EvaluationResultId, SamplingPolicyId, SampledAt, SampledBy, AssignedTo, Status,
     ReviewerComment, ReviewVerdict, ReviewedBy, ReviewedAt)
VALUES
    (@R1x, @SP1, '2026-02-10 10:00:00', 'system', 'analyst1', 'Reviewed',
     'I agree with the AI scoring. Sarah did a great job — the 3 on wrap-up seems right, she could have been more thorough summarising the actions.',
     'Agree', 'analyst1', '2026-02-10 15:30:00'),

    (@R2x, @SP1, '2026-02-11 12:00:00', 'system', 'analyst1', 'InReview',
     NULL, NULL, NULL, NULL),

    (@R3x, @SP2, '2026-02-12 15:00:00', 'system', 'analyst1', 'Pending',
     NULL, NULL, NULL, NULL);
GO

-- ── 5.14  CallPipeline — sample upload job ────────────────────────────────────
DECLARE @FormCSp INT = (SELECT Id FROM dbo.EvaluationForms WHERE Name = 'Inbound Customer Service QA Form');
DECLARE @Proj1   INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');

INSERT INTO dbo.CallPipelineJobs
    (Name, SourceType, FormId, ProjectId, Status,
     CreatedAt, StartedAt, CompletedAt, CreatedBy)
VALUES
    ('March 2026 CSV Upload Batch', 'FileUpload', @FormCSp, @Proj1, 'Completed',
     '2026-03-01 08:00:00', '2026-03-01 08:01:00', '2026-03-01 08:12:00', 'analyst1');

DECLARE @Job1 INT = SCOPE_IDENTITY();

DECLARE @R1p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Sarah Johnson' ORDER BY EvaluatedAt);
DECLARE @R2p INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Mark Davis'   ORDER BY EvaluatedAt);

INSERT INTO dbo.CallPipelineItems
    (JobId, SourceReference, AgentName, CallReference, CallDate, Status,
     CreatedAt, ProcessedAt, EvaluationResultId, ScorePercent, AiReasoning)
VALUES
    (@Job1, 'data:text/plain,(transcript%20text%20here)', 'Sarah Johnson', 'REF-20260210-001', '2026-02-10 09:00:00',
     'Completed', '2026-03-01 08:01:00', '2026-03-01 08:05:00', @R1p, 88.0,
     'Strong performance overall with minor wrap-up improvement needed.'),

    (@Job1, 'data:text/plain,(transcript%20text%20here)', 'Mark Davis', 'REF-20260211-007', '2026-02-11 11:15:00',
     'Completed', '2026-03-01 08:05:00', '2026-03-01 08:09:00', @R2p, 62.5,
     'Failed regulatory compliance and data verification — coaching required.'),

    (@Job1, 'data:text/plain,(transcript%20text%20here)', 'Tom Richards', 'REF-20260301-015', '2026-03-01 07:30:00',
     'Failed', '2026-03-01 08:09:00', '2026-03-01 08:10:00', NULL, NULL,
     NULL);

UPDATE dbo.CallPipelineItems
SET ErrorMessage = 'Transcript returned empty — file row may have been blank.'
WHERE JobId = @Job1 AND AgentName = 'Tom Richards';
GO

-- ── 5.15  Training Plans ──────────────────────────────────────────────────────
DECLARE @R2t INT = (SELECT TOP 1 Id FROM dbo.EvaluationResults WHERE AgentName = 'Mark Davis' ORDER BY EvaluatedAt);
DECLARE @HRI2 INT = (SELECT TOP 1 Id FROM dbo.HumanReviewItems WHERE EvaluationResultId = @R2t);
DECLARE @Proj1t INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');

INSERT INTO dbo.TrainingPlans
    (Title, Description, AgentName, AgentUsername, TrainerName, TrainerUsername,
     Status, DueDate, ProjectId, EvaluationResultId, HumanReviewItemId,
     CreatedBy, CreatedAt, UpdatedAt)
VALUES
    ('Compliance Remediation — Mark Davis',
     'Mark failed both data verification and the regulatory disclosure script on 11 Feb. '
     + 'This plan targets immediate compliance remediation with a structured coaching programme.',
     'Mark Davis', 'agent1',
     'Sarah Johnson', NULL,
     'Active', '2026-03-31 00:00:00',
     @Proj1t, @R2t, @HRI2,
     'qamanager', '2026-02-12 09:00:00', '2026-02-12 09:00:00');

DECLARE @TP1 INT = SCOPE_IDENTITY();

INSERT INTO dbo.TrainingPlanItems
    (TrainingPlanId, TargetArea, ItemType, Content, Status, [Order])
VALUES
    (@TP1, 'Compliance', 'Observation',
     'Agent failed to read the mandatory regulatory disclosure script at the start of the call. '
     + 'This is a critical compliance requirement and a zero-tolerance breach.',
     'Pending', 1),

    (@TP1, 'Compliance', 'Recommendation',
     'Complete the mandatory compliance e-learning module (Compliance-101) by 21 Feb 2026. '
     + 'Trainer to verify completion certificate.',
     'InProgress', 2),

    (@TP1, 'Compliance', 'Observation',
     'Data verification was attempted once (name only) rather than the required minimum of two identifiers.',
     'Pending', 3),

    (@TP1, 'Compliance', 'Recommendation',
     'Role-play three data verification scenarios with trainer by 28 Feb 2026 and score 100% on assessment.',
     'Pending', 4),

    (@TP1, 'Resolution', 'Observation',
     'Resolution provided was partially correct — agent quoted an outdated refund policy. '
     + 'Knowledge gap identified in billing resolution procedures.',
     'Pending', 5),

    (@TP1, 'Resolution', 'Recommendation',
     'Read the updated Billing Resolution Policy (v4, Jan 2026) and complete the policy knowledge check with a minimum score of 90%.',
     'Pending', 6);
GO

-- ── 5.16  Audit Log entries (sample PII and API call events) ─────────────────
DECLARE @Proj1al INT = (SELECT Id FROM dbo.Projects WHERE Name = 'Customer Service Excellence');

INSERT INTO dbo.AuditLogs
    (ProjectId, Category, EventType, Outcome, Actor, PiiTypesDetected,
     HttpMethod, Endpoint, HttpStatusCode, DurationMs, Provider, Details, OccurredAt)
VALUES
    (@Proj1al, 'PiiEvent', 'PiiDetected', 'Detected', 'auto-audit', 'EMAIL,PHONE',
     NULL, NULL, NULL, NULL, NULL,
     'PII detected in transcript for REF-20260211-007 before LLM call.',
     '2026-03-01 08:05:10'),

    (@Proj1al, 'PiiEvent', 'PiiRedacted', 'Redacted', 'auto-audit', 'EMAIL,PHONE',
     NULL, NULL, NULL, NULL, NULL,
     'PII tokens replaced with [EMAIL] and [PHONE] before LLM call. REF-20260211-007.',
     '2026-03-01 08:05:11'),

    (@Proj1al, 'ExternalApiCall', 'LlmAudit', 'Success', 'auto-audit', NULL,
     'POST', 'https://my-openai.openai.azure.com/openai/deployments/gpt-4o/chat/completions', 200, 3420, 'AzureOpenAI',
     'Job: 1; Item: 1; Ref: REF-20260210-001',
     '2026-03-01 08:05:05'),

    (@Proj1al, 'ExternalApiCall', 'LlmAudit', 'Success', 'auto-audit', NULL,
     'POST', 'https://my-openai.openai.azure.com/openai/deployments/gpt-4o/chat/completions', 200, 4110, 'AzureOpenAI',
     'Job: 1; Item: 2; Ref: REF-20260211-007',
     '2026-03-01 08:09:00'),

    (@Proj1al, 'ExternalApiCall', 'UrlFetch', 'Failure', 'pipeline:March 2026 CSV Upload Batch', NULL,
     'GET', '(inline transcript)', NULL, 0, NULL,
     'Job: 1; Item: 3; Error: Transcript returned empty.',
     '2026-03-01 08:09:30');
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
