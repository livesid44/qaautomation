-- =============================================================================
-- Seed Script: E.ON Germany Tenant
-- Source:  Quality Evaluation Form_EON Germany (based on 2 TRANSCRIPTS).xlsx
--          Revised Script with EON Germany Website References.docx
-- Target:  QAAutomation SQL Server database
-- Run:     Execute ONCE on a database that already has the full schema applied
--          (QAAutomation_MSSQL.sql).  Safe to re-run — each section checks for
--          existence before inserting.
-- =============================================================================

SET NOCOUNT ON;
GO

-- =============================================================================
-- SECTION 1 — AppUsers  (E.ON Germany team)
-- Passwords use SHA-256 hex (matches AppDbContext.HashPassword).
--   eon_admin    / EonAdmin@123
--   eon_manager  / EonManager@1
--   eon_analyst1 / EonAnalyst@1
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Username = 'eon_admin')
    INSERT INTO dbo.AppUsers (Username, PasswordHash, Email, Role, IsActive, CreatedAt)
    VALUES ('eon_admin',
            'b0e8fe38418c44fb7c6315653721281686246fbde0166cd1392e25dc788490b6',
            'eon_admin@eon-germany.local', 'Admin', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Username = 'eon_manager')
    INSERT INTO dbo.AppUsers (Username, PasswordHash, Email, Role, IsActive, CreatedAt)
    VALUES ('eon_manager',
            'ea3c01db873df730c3948df48fe265e13b859e9cc8b7a3ccb6bf266e226fba69',
            'eon_manager@eon-germany.local', 'Manager', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.AppUsers WHERE Username = 'eon_analyst1')
    INSERT INTO dbo.AppUsers (Username, PasswordHash, Email, Role, IsActive, CreatedAt)
    VALUES ('eon_analyst1',
            '0ab9fe0094478105ed24731c2eef92aa8dec872adb2b589895b4452f2a9a3264',
            'eon_analyst1@eon-germany.local', 'Analyst', 1, SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 2 — Project
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Projects WHERE Name = 'E.ON Germany')
    INSERT INTO dbo.Projects (Name, Description, IsActive, PiiProtectionEnabled, PiiRedactionMode, CreatedAt)
    VALUES ('E.ON Germany',
            'E.ON Energy Germany — Customer Service QA programme covering energy plan renewals, '
            + 'cancellations, and retention calls. Evaluated against GDPR, DPA, and E.ON process standards.',
            1, 1, 'Redact', SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 3 — UserProjectAccesses
-- All three E.ON users + the platform admin/qamanager/analyst1 get access.
-- =============================================================================

DECLARE @PEon INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

-- E.ON-specific users
INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, @PEon, SYSUTCDATETIME()
FROM   dbo.AppUsers u
WHERE  u.Username IN ('eon_admin', 'eon_manager', 'eon_analyst1')
  AND  NOT EXISTS (
       SELECT 1 FROM dbo.UserProjectAccesses x
       WHERE x.UserId = u.Id AND x.ProjectId = @PEon);

-- Platform-level admin/qamanager/analyst1 also get access
INSERT INTO dbo.UserProjectAccesses (UserId, ProjectId, GrantedAt)
SELECT u.Id, @PEon, SYSUTCDATETIME()
FROM   dbo.AppUsers u
WHERE  u.Username IN ('admin', 'qamanager', 'analyst1')
  AND  NOT EXISTS (
       SELECT 1 FROM dbo.UserProjectAccesses x
       WHERE x.UserId = u.Id AND x.ProjectId = @PEon);
GO

-- =============================================================================
-- SECTION 4 — Line of Business (LOB)
-- =============================================================================

DECLARE @PEon2 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

IF NOT EXISTS (SELECT 1 FROM dbo.Lobs WHERE Name = 'Energy Customer Service' AND ProjectId = @PEon2)
    INSERT INTO dbo.Lobs (ProjectId, Name, Description, IsActive, CreatedAt)
    VALUES (@PEon2,
            'Energy Customer Service',
            'Inbound energy customer service calls — renewals, cancellations, billing queries, and retention.',
            1, SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 5 — RatingCriteria and RatingLevels  (0 – 5 scale)
-- Mirrors the "Assessment Score (0-5)" column in the evaluation form.
-- =============================================================================

DECLARE @PEon3 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

IF NOT EXISTS (SELECT 1 FROM dbo.RatingCriteria WHERE Name = 'E.ON Germany Score (0–5)' AND ProjectId = @PEon3)
BEGIN
    INSERT INTO dbo.RatingCriteria (Name, Description, MinScore, MaxScore, IsActive, ProjectId, CreatedAt)
    VALUES ('E.ON Germany Score (0–5)',
            'Standard 0-to-5 scoring scale for all E.ON Germany QA evaluation criteria. '
            + '0 = behaviour completely absent; 5 = behaviour perfectly executed.',
            0, 5, 1, @PEon3, SYSUTCDATETIME());

    DECLARE @RC INT = SCOPE_IDENTITY();

    INSERT INTO dbo.RatingLevels (CriteriaId, Score, Label, Description, Color)
    VALUES
        (@RC, 0, 'Not Demonstrated',
         'The required behaviour was entirely absent from the interaction.',
         '#dc3545'),   -- red
        (@RC, 1, 'Very Poor',
         'Attempted but significantly below the required standard; major gaps in execution.',
         '#e07020'),   -- orange-red
        (@RC, 2, 'Below Standard',
         'Partially meets the requirement; noticeable gaps or errors remain.',
         '#fd7e14'),   -- orange
        (@RC, 3, 'Meets Standard',
         'Fully meets the minimum acceptable standard; no significant gaps.',
         '#ffc107'),   -- amber
        (@RC, 4, 'Above Standard',
         'Exceeds the requirement; strong execution with only minor areas for improvement.',
         '#20c997'),   -- teal
        (@RC, 5, 'Excellent',
         'Exemplary execution; a benchmark example for coaching and calibration.',
         '#198754');   -- green
END;
GO

-- =============================================================================
-- SECTION 6 — Parameters  (11 evaluation criteria from the form)
--
-- Criticality mapping:
--   Fatal     → DefaultWeight 0.12  (steps 1, 2, 7, 8, 10)
--   Non-Fatal → DefaultWeight 0.07  (steps 3, 5, 6, 9, 11)
--   Minor     → DefaultWeight 0.05  (step 4)
--
-- Category reflects the phase of the call where the criterion applies.
-- The Weighted Score formula from the form:
--   Weighted Score = (Weightage × Rating) / 5
-- Total of all weights = 1.00 → the normalised score range is 0–5 / 0–100%.
-- =============================================================================

DECLARE @PEon4 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

-- ── Call Opening ─────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Greeting Clarity and Professionalism' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Greeting Clarity and Professionalism',
            'Agent opens the call with the company name (E.ON Energy), their own name, and a clear '
            + 'GDPR recording notice. Greeting must be professional, warm, and set the right tone. '
            + 'Scoring guidance: 5 = all elements present and delivered naturally; '
            + '0 = generic or missing greeting with no GDPR notice.',
            'Call Opening', 0.12, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Identity Verification (DPA) After Purpose Disclosure' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Identity Verification (DPA) After Purpose Disclosure',
            'Agent explains the purpose of the call before performing Data Protection Act (DPA) '
            + 'identity verification. Verification must happen after the purpose is disclosed, not before. '
            + 'Scoring guidance: 5 = purpose clearly stated, then verification completed correctly; '
            + '0 = DPA skipped entirely or performed in wrong order.',
            'Call Opening', 0.12, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Clear Explanation of Call Purpose' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Clear Explanation of Call Purpose',
            'Agent confirms and clearly restates the customer''s reason for calling to ensure mutual '
            + 'understanding before proceeding. Should reflect the customer''s own words or intent. '
            + 'Scoring guidance: 5 = purpose confirmed and paraphrased accurately; '
            + '0 = agent proceeds without establishing the call reason.',
            'Call Opening', 0.07, 1, 'LLM', @PEon4, SYSUTCDATETIME());

-- ── Investigation & Communication ────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Complete and Structured Questioning' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Complete and Structured Questioning',
            'Agent uses structured, relevant questions to gather all necessary information before '
            + 'proposing a solution. Questions should be logical, non-repetitive, and focused. '
            + 'Scoring guidance: 5 = systematic questioning that covers all relevant angles; '
            + '0 = no meaningful questions asked, agent assumes without investigating.',
            'Investigation & Communication', 0.05, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Addressing Customer Issue or Reason' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Addressing Customer Issue or Reason',
            'Agent tailors their response directly to the customer''s specific concern or reason for '
            + 'calling. Response must be personalised, not generic. '
            + 'Scoring guidance: 5 = response fully addresses the customer''s unique situation; '
            + '0 = agent gives a generic response unrelated to the actual issue.',
            'Investigation & Communication', 0.07, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Use of Clear and Professional Language' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Use of Clear and Professional Language',
            'Agent avoids slang, technical jargon, and filler words. Uses polite, clear language '
            + 'appropriate to a professional energy customer service interaction. '
            + 'Scoring guidance: 5 = consistently professional, articulate, and adapted to the customer; '
            + '0 = inappropriate language, excessive filler, or unprofessional tone throughout.',
            'Investigation & Communication', 0.07, 1, 'LLM', @PEon4, SYSUTCDATETIME());

-- ── Regulatory & Consent ─────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Transparent Rate and Process Explanation' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Transparent Rate and Process Explanation',
            'Agent proactively explains any changes to energy rates, fees, or contract terms clearly '
            + 'and accurately, including the reasons for any price increase. Does not withhold or '
            + 'minimise information the customer would consider material. '
            + 'Scoring guidance: 5 = rates, fees, and process fully explained with supporting reasons; '
            + '0 = rate or process information is omitted, inaccurate, or actively misleading.',
            'Regulatory & Consent', 0.12, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Explicit Consent for Callback and Contact Use' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Explicit Consent for Callback and Contact Use',
            'Before arranging any follow-up call or using the customer''s contact details, the agent '
            + 'explicitly asks for and receives the customer''s permission. Consent must not be assumed. '
            + 'Scoring guidance: 5 = consent sought, received, and confirmed before any follow-up is scheduled; '
            + '0 = follow-up arranged or contact use implied without consent.',
            'Regulatory & Consent', 0.12, 1, 'LLM', @PEon4, SYSUTCDATETIME());

-- ── Retention & Compliance ───────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Retention Efforts (If Applicable)' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Retention Efforts (If Applicable)',
            'Where the customer expresses dissatisfaction, intent to cancel, or a desire to switch, '
            + 'the agent makes a genuine, value-based retention attempt — offering a relevant discount, '
            + 'plan, or benefit — without being pushy or dismissive. '
            + 'Scoring guidance: 5 = proactive, empathetic retention attempt with a relevant offer made; '
            + '3 = attempt made but generic; 0 = no retention attempt on a clearly at-risk call. '
            + 'Mark N/A only where there is genuinely no retention opportunity in the call.',
            'Retention & Compliance', 0.07, 1, 'LLM', @PEon4, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Adherence to Process and Accuracy' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Adherence to Process and Accuracy',
            'Agent correctly follows E.ON''s defined service process — including the switching process, '
            + 'contract modification steps, and regulatory requirements — and provides accurate '
            + 'information throughout. No shortcuts that could create compliance risk or customer harm. '
            + 'Scoring guidance: 5 = process followed correctly end-to-end with full accuracy; '
            + '0 = process violated or material inaccuracy given to the customer.',
            'Retention & Compliance', 0.12, 1, 'LLM', @PEon4, SYSUTCDATETIME());

-- ── Call Closure ─────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM dbo.Parameters WHERE Name = 'Professional and Complete Call Closure' AND ProjectId = @PEon4)
    INSERT INTO dbo.Parameters (Name, Description, Category, DefaultWeight, IsActive, EvaluationType, ProjectId, CreatedAt)
    VALUES ('Professional and Complete Call Closure',
            'Agent recaps the agreed next steps, confirms any actions taken or scheduled, thanks '
            + 'the customer by name, and ends the call positively. Customer should leave the call '
            + 'clear on what happens next. '
            + 'Scoring guidance: 5 = full recap, next steps confirmed, warm professional close; '
            + '0 = call ends abruptly with no summary or confirmation of next steps.',
            'Call Closure', 0.07, 1, 'LLM', @PEon4, SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 7 — ParameterClubs  (grouped by criticality level)
-- Fatal items can auto-fail the call; Non-Fatal items affect score but do not
-- auto-fail; Minor items are tracked but carry the lowest weight.
-- =============================================================================

DECLARE @PEon5 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

IF NOT EXISTS (SELECT 1 FROM dbo.ParameterClubs WHERE Name = 'E.ON Germany — Fatal Parameters' AND ProjectId = @PEon5)
BEGIN
    INSERT INTO dbo.ParameterClubs (Name, Description, IsActive, ProjectId, CreatedAt)
    VALUES ('E.ON Germany — Fatal Parameters',
            'The five Fatal criteria (weight 0.12 each, total 0.60). A score of 0 on any Fatal '
            + 'parameter constitutes a critical failure and may trigger immediate coaching regardless '
            + 'of the overall weighted score. Covers: Greeting/GDPR, DPA verification, '
            + 'Rate/Process transparency, Consent, and Process adherence.',
            1, @PEon5, SYSUTCDATETIME());

    DECLARE @ClubFatal INT = SCOPE_IDENTITY();

    INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride, RatingCriteriaId)
    VALUES
        (@ClubFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Greeting Clarity and Professionalism'                AND ProjectId = @PEon5),
         0, NULL, NULL),
        (@ClubFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Identity Verification (DPA) After Purpose Disclosure' AND ProjectId = @PEon5),
         1, NULL, NULL),
        (@ClubFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Transparent Rate and Process Explanation'             AND ProjectId = @PEon5),
         2, NULL, NULL),
        (@ClubFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Explicit Consent for Callback and Contact Use'        AND ProjectId = @PEon5),
         3, NULL, NULL),
        (@ClubFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Adherence to Process and Accuracy'                    AND ProjectId = @PEon5),
         4, NULL, NULL);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ParameterClubs WHERE Name = 'E.ON Germany — Non-Fatal Parameters' AND ProjectId = @PEon5)
BEGIN
    INSERT INTO dbo.ParameterClubs (Name, Description, IsActive, ProjectId, CreatedAt)
    VALUES ('E.ON Germany — Non-Fatal Parameters',
            'The five Non-Fatal criteria (weight 0.07 each, total 0.35). A low score impacts the '
            + 'overall weighted percentage but does not auto-fail the call. Covers: Call purpose, '
            + 'Issue addressing, Professional language, Retention efforts, and Call closure.',
            1, @PEon5, SYSUTCDATETIME());

    DECLARE @ClubNonFatal INT = SCOPE_IDENTITY();

    INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride, RatingCriteriaId)
    VALUES
        (@ClubNonFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Clear Explanation of Call Purpose'          AND ProjectId = @PEon5),
         0, NULL, NULL),
        (@ClubNonFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Addressing Customer Issue or Reason'        AND ProjectId = @PEon5),
         1, NULL, NULL),
        (@ClubNonFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Use of Clear and Professional Language'     AND ProjectId = @PEon5),
         2, NULL, NULL),
        (@ClubNonFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Retention Efforts (If Applicable)'          AND ProjectId = @PEon5),
         3, NULL, NULL),
        (@ClubNonFatal,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Professional and Complete Call Closure'     AND ProjectId = @PEon5),
         4, NULL, NULL);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.ParameterClubs WHERE Name = 'E.ON Germany — Minor Parameters' AND ProjectId = @PEon5)
BEGIN
    INSERT INTO dbo.ParameterClubs (Name, Description, IsActive, ProjectId, CreatedAt)
    VALUES ('E.ON Germany — Minor Parameters',
            'The one Minor criterion (weight 0.05). Tracked for coaching but carries the lowest '
            + 'weight and does not by itself affect pass/fail outcomes.',
            1, @PEon5, SYSUTCDATETIME());

    DECLARE @ClubMinor INT = SCOPE_IDENTITY();

    INSERT INTO dbo.ParameterClubItems (ClubId, ParameterId, [Order], WeightOverride, RatingCriteriaId)
    VALUES
        (@ClubMinor,
         (SELECT Id FROM dbo.Parameters WHERE Name = 'Complete and Structured Questioning' AND ProjectId = @PEon5),
         0, NULL, NULL);
END;
GO

-- =============================================================================
-- SECTION 8 — EvaluationForm, FormSections, and FormFields
--
-- Form layout follows the sequence in the source XLSX (Steps 1–11), organised
-- into five sections that map to the natural phases of an energy service call.
--
-- FieldType = 2 (Rating)  →  MaxRating = 5  →  matches Assessment Score 0–5.
-- The form Description field stores Gemini's scoring prompt guidance.
-- =============================================================================

DECLARE @LobEon INT = (SELECT Id FROM dbo.Lobs
                       WHERE  Name = 'Energy Customer Service'
                         AND  ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany'));

IF NOT EXISTS (SELECT 1 FROM dbo.EvaluationForms WHERE Name = 'E.ON Germany Energy Service QA Form')
BEGIN
    INSERT INTO dbo.EvaluationForms (Name, Description, IsActive, LobId, CreatedAt, UpdatedAt)
    VALUES ('E.ON Germany Energy Service QA Form',
            'Quality evaluation form for E.ON Germany inbound energy customer service calls. '
            + 'Covers GDPR compliance, DPA verification, rate/process transparency, consent, '
            + 'retention, and call closure. Scoring: 0–5 per criterion with weighted aggregation '
            + '(Fatal weight 0.12, Non-Fatal weight 0.07, Minor weight 0.05; total weights = 1.00). '
            + 'Normalised Score = (Total Weighted Score / Sum of Weights) × 5. '
            + 'Pass threshold: >= 3.0 normalised (60%). Any Fatal criterion at 0 = critical failure.',
            1, @LobEon, SYSUTCDATETIME(), SYSUTCDATETIME());

    DECLARE @Form INT = SCOPE_IDENTITY();

    -- ── Section 1: Call Opening ───────────────────────────────────────────────
    INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
    VALUES (@Form,
            'Call Opening',
            'Steps 1–3: How the agent opens the call — greeting, GDPR notice, DPA verification, '
            + 'and purpose disclosure. Contains two Fatal criteria and one Non-Fatal criterion.',
            0);

    DECLARE @S1 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
    VALUES
        (@S1, 'Greeting Clarity and Professionalism',
         2, 1, 0, 5,
         'FATAL (weight 0.12). Evaluate whether the agent opened with: (1) E.ON Energy company name, '
         + '(2) their own name, and (3) a clear GDPR recording notice. All three elements must be present '
         + 'and delivered professionally. Score 5 if all present and natural; score 0 if GDPR notice '
         + 'omitted entirely or if the greeting was unprofessional.'),

        (@S1, 'Identity Verification (DPA) After Purpose Disclosure',
         2, 1, 1, 5,
         'FATAL (weight 0.12). The agent must disclose the purpose of the call before asking verification '
         + 'questions. Verify the sequence: purpose disclosed → DPA verification performed. '
         + 'Score 5 if sequence correct and verification complete; score 0 if verification skipped '
         + 'or performed before purpose was disclosed.'),

        (@S1, 'Clear Explanation of Call Purpose',
         2, 1, 2, 5,
         'NON-FATAL (weight 0.07). Did the agent confirm and restate the customer''s reason for calling '
         + 'in their own words, demonstrating understanding? Score 5 if purpose confirmed and '
         + 'paraphrased accurately; score 0 if agent proceeded without establishing the call reason.');

    -- ── Section 2: Investigation & Communication ──────────────────────────────
    INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
    VALUES (@Form,
            'Investigation and Communication',
            'Steps 4–6: How the agent investigates the customer''s situation and communicates during '
            + 'the interaction. Contains one Minor criterion and two Non-Fatal criteria.',
            1);

    DECLARE @S2 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
    VALUES
        (@S2, 'Complete and Structured Questioning',
         2, 1, 0, 5,
         'MINOR (weight 0.05). Did the agent use logical, structured questions to gather relevant '
         + 'information before offering a solution? Penalise repetitive, irrelevant, or leading questions. '
         + 'Score 5 for systematic questioning that builds a complete picture; score 0 if the agent '
         + 'made assumptions and offered solutions without any meaningful investigation.'),

        (@S2, 'Addressing Customer Issue or Reason',
         2, 1, 1, 5,
         'NON-FATAL (weight 0.07). Did the agent tailor their response specifically to this customer''s '
         + 'stated concern? A generic response not connected to the customer''s situation scores low. '
         + 'Score 5 if response fully addresses the customer''s unique situation; '
         + 'score 0 if response is generic and unrelated to the actual issue.'),

        (@S2, 'Use of Clear and Professional Language',
         2, 1, 2, 5,
         'NON-FATAL (weight 0.07). Did the agent avoid slang, excessive jargon, and filler words '
         + 'throughout the call? Language should be polite, clear, and adapted to the customer''s level. '
         + 'Score 5 for consistently professional, articulate communication; '
         + 'score 0 for inappropriate language, unprofessional tone, or excessive filler throughout.');

    -- ── Section 3: Regulatory & Consent ──────────────────────────────────────
    INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
    VALUES (@Form,
            'Regulatory and Consent',
            'Steps 7–8: Rate/process transparency and explicit consent for follow-up contact. '
            + 'Both criteria are Fatal — they carry the highest weight and compliance risk.',
            2);

    DECLARE @S3 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
    VALUES
        (@S3, 'Transparent Rate and Process Explanation',
         2, 1, 0, 5,
         'FATAL (weight 0.12). Did the agent proactively explain changes in energy rates, fees, or '
         + 'contract terms, including the reason for any price increases? Information must be accurate '
         + 'and complete — omitting material information is a Fatal failure. '
         + 'Reference: E.ON price change information at https://www.eon.de/de/eonerleben/preisanpassung.html. '
         + 'Score 5 if all relevant rate/process information provided accurately and proactively; '
         + 'score 0 if rate information is omitted, inaccurate, or actively misleading.'),

        (@S3, 'Explicit Consent for Callback and Contact Use',
         2, 1, 1, 5,
         'FATAL (weight 0.12). Before arranging a callback or using the customer''s contact details, '
         + 'did the agent explicitly ask for and receive the customer''s permission? Consent must be '
         + 'voluntary and clearly confirmed — it cannot be assumed or implied. '
         + 'Score 5 if consent clearly sought, received, and confirmed before follow-up was arranged; '
         + 'score 0 if callback was arranged or contact use implied without any consent request.');

    -- ── Section 4: Retention & Compliance ────────────────────────────────────
    INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
    VALUES (@Form,
            'Retention and Compliance',
            'Steps 9–10: Retention effort where applicable, and adherence to E.ON process and '
            + 'regulatory requirements. Contains one Non-Fatal and one Fatal criterion.',
            3);

    DECLARE @S4 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
    VALUES
        (@S4, 'Retention Efforts (If Applicable)',
         2, 0, 0, 5,
         'NON-FATAL (weight 0.07). On calls where the customer expresses dissatisfaction, intent to cancel, '
         + 'or a desire to switch provider, did the agent make a genuine, value-based retention attempt? '
         + 'The offer must be relevant and empathetic — not scripted or pushy. '
         + 'Example offers: loyalty discount, fixed-rate plan, solar consultation (https://www.eon.de/de/pk/solar.html). '
         + 'Score 5 for a proactive, personalised retention attempt; score 3 if attempted but generic; '
         + 'score 0 for no attempt on a clearly at-risk call. Mark N/A if no retention opportunity exists.'),

        (@S4, 'Adherence to Process and Accuracy',
         2, 1, 1, 5,
         'FATAL (weight 0.12). Did the agent correctly follow E.ON''s defined processes — including '
         + 'the correct switching/cancellation process, contract modification steps, and all applicable '
         + 'regulatory requirements? Was all information provided to the customer accurate? '
         + 'Correct switching process: customer contacts new provider; E.ON account closes automatically '
         + '(https://www.eon.de/de/pk/strom/stromanbieter-wechsel.html). '
         + 'Score 5 for full process compliance and complete accuracy throughout; '
         + 'score 0 if the process was violated or materially inaccurate information was given.');

    -- ── Section 5: Call Closure ───────────────────────────────────────────────
    INSERT INTO dbo.FormSections (FormId, Title, Description, [Order])
    VALUES (@Form,
            'Call Closure',
            'Step 11: How the agent closes the call — recap of next steps, confirmation of actions, '
            + 'and professional sign-off. Non-Fatal criterion.',
            4);

    DECLARE @S5 INT = SCOPE_IDENTITY();

    INSERT INTO dbo.FormFields (SectionId, Label, FieldType, IsRequired, [Order], MaxRating, Description)
    VALUES
        (@S5, 'Professional and Complete Call Closure',
         2, 1, 0, 5,
         'NON-FATAL (weight 0.07). Did the agent close the call by: (1) recapping the agreed next steps '
         + 'or actions taken, (2) confirming any scheduled follow-up or plan change, '
         + '(3) thanking the customer by name, and (4) ending positively? '
         + 'The customer should leave the call clear on what happens next. '
         + 'Score 5 for a full, warm, structured closure; '
         + 'score 0 if the call ends abruptly with no recap or confirmation of next steps.');
END;
GO

-- =============================================================================
-- SECTION 9 — Sampling Policies
-- =============================================================================

DECLARE @PEon6 INT = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

IF NOT EXISTS (SELECT 1 FROM dbo.SamplingPolicies WHERE Name = 'E.ON Germany — Random 10% Sample' AND ProjectId = @PEon6)
    INSERT INTO dbo.SamplingPolicies
        (Name, Description, ProjectId, CallTypeFilter, SamplingMethod, SampleValue,
         IsActive, CreatedBy, CreatedAt, UpdatedAt)
    VALUES
        ('E.ON Germany — Random 10% Sample',
         'Random 10% sample of all E.ON Germany calls for general quality monitoring.',
         @PEon6, NULL, 'Percentage', 10,
         1, 'eon_admin', SYSUTCDATETIME(), SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.SamplingPolicies WHERE Name = 'E.ON Germany — Fatal-Risk 100% Review' AND ProjectId = @PEon6)
    INSERT INTO dbo.SamplingPolicies
        (Name, Description, ProjectId, CallTypeFilter, SamplingMethod, SampleValue,
         IsActive, CreatedBy, CreatedAt, UpdatedAt)
    VALUES
        ('E.ON Germany — Fatal-Risk 100% Review',
         '100% human review of any call where one or more Fatal criteria scored 0 or 1. '
         + 'Ensures every GDPR, DPA, consent, or process-adherence failure receives QA analyst attention.',
         @PEon6, 'FatalRisk', 'Percentage', 100,
         1, 'eon_admin', SYSUTCDATETIME(), SYSUTCDATETIME());
GO

-- =============================================================================
-- SECTION 10 — Verification
-- Run these queries after executing the script to confirm all rows were created.
-- =============================================================================

SELECT 'Project'          AS Entity, Name  AS Value FROM dbo.Projects        WHERE Name = 'E.ON Germany'
UNION ALL
SELECT 'LOB',              Name FROM dbo.Lobs
WHERE  ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany')
UNION ALL
SELECT 'EvaluationForm',   Name FROM dbo.EvaluationForms
WHERE  LobId IN (SELECT Id FROM dbo.Lobs WHERE ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany'))
UNION ALL
SELECT 'AppUser',          Username FROM dbo.AppUsers WHERE Username IN ('eon_admin','eon_manager','eon_analyst1');

-- Parameter count (expect 11)
SELECT 'Parameters created' AS Check, COUNT(*) AS Count
FROM dbo.Parameters
WHERE ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

-- RatingCriteria + RatingLevels (expect 1 criteria, 6 levels)
SELECT 'RatingCriteria'    AS Check, COUNT(*) AS Count
FROM dbo.RatingCriteria
WHERE ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany')
UNION ALL
SELECT 'RatingLevels', COUNT(*)
FROM dbo.RatingLevels
WHERE CriteriaId IN (
    SELECT Id FROM dbo.RatingCriteria
    WHERE ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany'));

-- ParameterClubs (expect 3 clubs, 11 items total)
SELECT pc.Name AS ClubName, COUNT(pci.Id) AS ItemCount
FROM   dbo.ParameterClubs pc
LEFT   JOIN dbo.ParameterClubItems pci ON pci.ClubId = pc.Id
WHERE  pc.ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany')
GROUP  BY pc.Id, pc.Name
ORDER  BY pc.Name;
-- Expected:
--   E.ON Germany — Fatal Parameters      5 items
--   E.ON Germany — Minor Parameters      1 item
--   E.ON Germany — Non-Fatal Parameters  5 items

-- FormSections and FormField counts (expect 5 sections, 11 fields total)
SELECT fs.Title AS SectionTitle, fs.[Order] AS SectionOrder, COUNT(ff.Id) AS FieldCount
FROM   dbo.FormSections fs
LEFT   JOIN dbo.FormFields ff ON ff.SectionId = fs.Id
INNER  JOIN dbo.EvaluationForms ef ON ef.Id = fs.FormId
WHERE  ef.Name = 'E.ON Germany Energy Service QA Form'
GROUP  BY fs.Id, fs.Title, fs.[Order]
ORDER  BY fs.[Order];
-- Expected:
--   Call Opening                      Order 0   3 fields
--   Investigation and Communication   Order 1   3 fields
--   Regulatory and Consent            Order 2   2 fields
--   Retention and Compliance          Order 3   2 fields
--   Call Closure                      Order 4   1 field

-- Sampling policies (expect 2)
SELECT Name, SamplingMethod, SampleValue
FROM   dbo.SamplingPolicies
WHERE  ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany');

-- UserProjectAccess (expect admin+qamanager+analyst1+eon_admin+eon_manager+eon_analyst1 = 6)
SELECT u.Username, u.Role
FROM   dbo.UserProjectAccesses upa
JOIN   dbo.AppUsers u ON u.Id = upa.UserId
WHERE  upa.ProjectId = (SELECT Id FROM dbo.Projects WHERE Name = 'E.ON Germany')
ORDER  BY u.Username;
GO
