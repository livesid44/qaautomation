-- =============================================================================
-- QAAutomation  –  Migration: Switch default AI provider to Google Gemini
-- =============================================================================
-- Purpose  : Updates the AiConfigs singleton row on an existing database to use
--            Google Gemini as the LLM, sentiment, and speech provider instead of
--            Azure OpenAI / Azure Cognitive Services.
--
--            Only fields that are still set to the original Azure defaults are
--            updated — any values you have already customised are left untouched.
--
-- Safe to re-run: all UPDATE statements are guarded by WHERE conditions so
--            executing this script a second time is a no-op.
--
-- Usage    : Run against your existing QAAutomation database:
--            sqlcmd -S <server> -d QAAutomation -i Migrate_To_Gemini.sql
--            (or execute via SSMS / Azure Data Studio)
--
-- Note     : If you have not yet run SqlServer_Migration.sql against this
--            database, run it first — it contains additional schema fixes
--            such as widening CallPipelineItems.SourceReference to
--            NVARCHAR(MAX), which is required for file-upload jobs.
-- =============================================================================

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ── Guard: AiConfigs table must exist ────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[AiConfigs]') AND type = N'U'
)
BEGIN
    RAISERROR(
        N'Table [dbo].[AiConfigs] does not exist. '
        + N'Run the full migration script (SqlServer_Migration.sql) first.',
        16, 1
    );
    RETURN;
END
GO

-- ── Guard: singleton row must exist ──────────────────────────────────────────
-- If the row is missing entirely, insert the Gemini defaults from scratch.
IF NOT EXISTS (SELECT 1 FROM [dbo].[AiConfigs] WHERE [Id] = 1)
BEGIN
    INSERT INTO [dbo].[AiConfigs]
        ([Id], [LlmProvider], [LlmEndpoint], [LlmApiKey], [LlmDeployment], [LlmTemperature],
         [SentimentProvider], [LanguageEndpoint], [LanguageApiKey], [RagTopK],
         [SpeechEndpoint], [SpeechApiKey], [GoogleApiKey], [GoogleGeminiModel],
         [SpeechProvider], [UpdatedAt])
    VALUES
        (1, N'Google', N'', N'', N'gemini-1.5-pro', 0.1,
         N'Google', N'', N'', 3,
         N'', N'', N'', N'gemini-1.5-pro',
         N'Google', SYSUTCDATETIME());

    PRINT N'[INFO] AiConfigs row did not exist — inserted with Gemini defaults.';
END
GO

-- =============================================================================
-- 1. LLM Provider
--    Switch from AzureOpenAI (or OpenAI Direct) → Google Gemini
-- =============================================================================
UPDATE [dbo].[AiConfigs]
SET
    [LlmProvider]   = N'Google',
    -- Only reset the deployment name when it is still the Azure/OpenAI default.
    -- If someone has already set a custom model name, keep it.
    [LlmDeployment] = CASE
                          WHEN [LlmDeployment] IN (N'gpt-4o', N'gpt-4', N'gpt-35-turbo', N'')
                          THEN N'gemini-1.5-pro'
                          ELSE [LlmDeployment]
                      END,
    [UpdatedAt]     = SYSUTCDATETIME()
WHERE [Id] = 1
  AND [LlmProvider] IN (N'AzureOpenAI', N'OpenAI');

IF @@ROWCOUNT > 0
    PRINT N'[UPDATED] LlmProvider switched to Google Gemini.';
ELSE
    PRINT N'[SKIPPED] LlmProvider already set to Google — no change needed.';
GO

-- =============================================================================
-- 2. Sentiment Provider
--    Switch from AzureOpenAI / OpenAI / AzureLanguage → Google Gemini
-- =============================================================================
UPDATE [dbo].[AiConfigs]
SET
    [SentimentProvider] = N'Google',
    [UpdatedAt]         = SYSUTCDATETIME()
WHERE [Id] = 1
  AND [SentimentProvider] IN (N'AzureOpenAI', N'OpenAI', N'AzureLanguage');

IF @@ROWCOUNT > 0
    PRINT N'[UPDATED] SentimentProvider switched to Google Gemini.';
ELSE
    PRINT N'[SKIPPED] SentimentProvider already set to Google — no change needed.';
GO

-- =============================================================================
-- 3. Speech Provider
--    Switch from Azure Cognitive Services → Google Cloud Speech-to-Text
-- =============================================================================
UPDATE [dbo].[AiConfigs]
SET
    [SpeechProvider] = N'Google',
    [UpdatedAt]      = SYSUTCDATETIME()
WHERE [Id] = 1
  AND [SpeechProvider] = N'Azure';

IF @@ROWCOUNT > 0
    PRINT N'[UPDATED] SpeechProvider switched to Google Cloud STT.';
ELSE
    PRINT N'[SKIPPED] SpeechProvider already set to Google — no change needed.';
GO

-- =============================================================================
-- 4. Set GoogleGeminiModel if it is still blank or unset
-- =============================================================================
UPDATE [dbo].[AiConfigs]
SET
    [GoogleGeminiModel] = N'gemini-1.5-pro',
    [UpdatedAt]         = SYSUTCDATETIME()
WHERE [Id] = 1
  AND ([GoogleGeminiModel] IS NULL OR [GoogleGeminiModel] = N'');

IF @@ROWCOUNT > 0
    PRINT N'[UPDATED] GoogleGeminiModel set to gemini-1.5-pro.';
ELSE
    PRINT N'[SKIPPED] GoogleGeminiModel already configured — no change needed.';
GO

-- =============================================================================
-- 5. Summary
-- =============================================================================
SELECT
    [Id],
    [LlmProvider],
    [LlmDeployment],
    [LlmTemperature],
    [SentimentProvider],
    [SpeechProvider],
    [GoogleGeminiModel],
    CASE WHEN [GoogleApiKey] <> N'' THEN N'*** (set)' ELSE N'(not set)' END AS [GoogleApiKey],
    [UpdatedAt]
FROM [dbo].[AiConfigs]
WHERE [Id] = 1;
GO

PRINT N'';
PRINT N'Migration complete.';
PRINT N'IMPORTANT: If GoogleApiKey is shown as "(not set)" above, you must';
PRINT N'configure your Google API Key via the AI Settings page in the application';
PRINT N'(Admin → AI Settings → Google AI Configuration).';
GO
