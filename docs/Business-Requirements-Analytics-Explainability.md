# Business Requirements — Analytics & Explainability
## TechM AI QA Automation Platform

**Document type:** Business Requirements Description  
**Audience:** QA Operations Leadership, Product Owners, Business Stakeholders, Compliance Teams  
**Status:** Draft — April 2026  
**Related documents:** TechM-AI-QA-Design-Doc-Descriptive-Copy.md · Outcomes-Measurement-Analytics-Explainability.txt

---

## 1. Why This Platform Needs Analytics & Explainability

The QA Automation platform replaces manual call evaluations with AI-driven scoring. This creates an immediate business obligation: **if the AI is making quality judgements that affect agent performance reviews, coaching plans, and compliance records, the organisation must be able to explain and defend those judgements.**

Two categories of business need drive the analytics suite:

**Operational need — run the QA team better.** QA Managers need to see, at a glance, how quality is trending across agents, call types, and evaluation criteria — without waiting for end-of-month reports. They need to answer questions like: *Are scores improving? Which agent needs coaching today? Which part of our evaluation form is driving the most failures?*

**Governance need — trust and defend the AI.** Compliance officers, team leaders, and ultimately regulators may ask: *Why was this call rated as failing? Did the AI score consistently and fairly? When a human analyst disagreed with the AI, what was the disagreement pattern?* The platform must be able to answer these questions with an audit trail, not just a number.

---

## 2. Stakeholders & Their Primary Questions

| Stakeholder | Role | Primary business questions |
|---|---|---|
| QA Manager | Operates the QA team day-to-day | Are overall scores trending up or down? Which agents need coaching now? Are we meeting our quality targets? |
| QA Team Leader | Coaches individual agents | Which specific evaluation criteria are my agents missing? Is coaching working — are scores improving week-on-week? |
| QA Analyst | Reviews AI-scored calls | How much do I typically change AI scores? Are AI suggestions reliable enough to trust? |
| Compliance Officer | Ensures regulatory adherence | Can we prove why a call was rated failing? Is the AI making consistent, unbiased decisions? |
| Operations Director | Reports on contact-centre quality | What is our overall QA pass rate? What is the top driver of failed calls? |
| TechM GVO Team | Governs the pilot programme | Is the platform delivering the promised 60% effort reduction? What are the highest risks right now? |

---

## 3. Analytics Dashboard — The QA Manager's Daily View

**Business objective:** Give QA Managers and Operations Directors a single screen that answers: *"How is our QA quality performing, and where do I need to focus attention today?"*

### What it shows

**Overall health badge.** A single status indicator — Good, Needs Attention, or Critical — based on the period's average quality score. A QA Manager opening the dashboard in the morning knows immediately whether the team is on track or whether intervention is needed, without reading a report.

**Daily quality score trend.** A chart showing average QA score percentage for each day. This is the primary operational heartbeat. A drop on a specific date can be correlated with a new evaluation form rollout, a high volume of complaint calls, or a change in agent team composition — providing context for management conversations.

**Agent performance ranking.** Every agent ranked by their average QA score. The platform automatically identifies the top-performing agents (who can be recognised and whose techniques can be shared) and the lowest-performing agents (who need immediate coaching support). A score distribution view groups agents into High, Medium, and Low bands so managers can see the overall health of the team at a glance, not just individual rankings.

**Evaluation criterion (parameter) trends.** Which specific QA criteria — for example, "Used customer name", "Offered callback", "Completed compliance disclosure" — are being consistently missed or passed? A QA Manager can see immediately that *Compliance Disclosure* is failing across 40% of calls, identifying a systemic training need rather than an individual agent problem.

**Call type performance.** Quality scores broken down by the type of evaluation form used — for example, Sales calls, Complaint handling, Technical support. This prevents unfair agent comparisons: an agent predominantly handling complaint calls should be compared against other complaint-call agents, not mixed with sales agents on easier evaluation forms.

**Agent improvement tracking.** Which agents are improving week-on-week, and which are declining? The most-improved agent of the period is surfaced automatically, enabling positive recognition. The most-declining agent is flagged, enabling proactive intervention before the decline becomes a performance issue.

**Section-level overview.** Quality scores broken down by section of the evaluation form — Opening, Compliance, Resolution, Closing, for example. This shows QA leadership whether the team is strong in greeting customers but weak in compliance steps, or vice versa, enabling targeted team-wide training priorities.

### AI-Generated Narrative Insights

Each chart section also provides a short, plain-English AI-generated summary — for example: *"Scores improved steadily over the last four weeks, with Compliance Disclosure remaining the most-missed criterion at 52%. Agent performance dispersion is narrowing, suggesting coaching interventions from Sprint 2 are taking effect."*

A **Leadership Summary** consolidates all chart insights into a single management-ready paragraph, suitable for copying into a weekly report or steering pack.

### Business value delivered

- Eliminates the need to export data to spreadsheets for weekly management reports
- Enables proactive coaching decisions based on live data rather than monthly batch reports
- Gives leadership a defensible, data-driven basis for agent performance conversations
- Reduces time to identify systemic training needs from weeks to minutes

---

## 4. Explainability Analytics — Why Did the AI Make That Decision?

**Business objective:** Give QA Managers, Compliance Officers, and Team Leaders the ability to answer the question: *"Why was this call — or this group of calls — scored the way it was?"*

This moves beyond reporting *what* the scores are, to explaining *why* they are what they are. This is critical for regulatory compliance and for building team trust in the AI system.

### Key performance indicators at a glance

The page opens with four headline numbers:

- **Total audits analysed** — the size of the data set behind the analysis, so stakeholders know whether insights are based on 10 calls or 10,000
- **AI–Human agreement rate** — the percentage of times a human analyst reviewed an AI score and agreed with it unchanged. A high rate (target: 85% or above) means the AI is reliably producing scores that QA analysts endorse. A declining rate is a warning signal that the AI may need recalibration
- **Parameters flagged as risk areas** — how many evaluation criteria are consistently being failed. These are the actionable coaching priorities
- **Human reviews completed** — how actively the QA team is engaging with the AI's suggestions, confirming the system is being used as intended

### Decision Drivers — what is actually causing calls to fail?

The platform analyses every evaluation parameter and shows which criteria most frequently drove failing outcomes. This answers the question: *"If we want to improve our pass rate fastest, which criteria should we coach on first?"*

A QA Manager can see, for example, that *Offer of Callback* is the single parameter most responsible for failed audits — contributing to 38% of all failures — and direct the next training session specifically at that behaviour, rather than running a general quality training session that covers everything equally.

### Human–AI agreement breakdown

This analysis shows, for each group of calls reviewed by a human analyst, how often the analyst agreed with the AI score, partially adjusted it, or rejected it entirely. It is broken down by the type of calls reviewed (the sampling policy).

This answers the compliance question: *"Can we show that a human is meaningfully overseeing the AI's decisions?"* A healthy pattern shows the AI handling the majority of straightforward calls reliably, with human review concentrated on complex or borderline cases. A high rejection rate on a specific call type is a signal that the AI needs better guidance for that category.

### Signal utilisation — are agents consistently meeting criteria?

For each evaluation criterion, the platform shows two business signals:

- **Full-score rate** — the proportion of calls where the agent achieved the maximum available marks on that criterion. A high rate confirms agents have genuinely mastered this behaviour
- **Miss rate** — the proportion of calls where the agent scored zero on that criterion. A consistently high miss rate — for example, 60% of calls missing *Compliance Disclosure* — is an unambiguous, urgent training priority

The combination of these two rates reveals the nature of the gap. A criterion with a high miss rate but also a high full-score rate has a bimodal pattern: some agents consistently achieve it and others consistently miss it, pointing to an individual coaching need. A criterion with high miss rates across the board points to a process or knowledge gap that affects the whole team.

### Failure reason ranking

The platform produces a ranked list of evaluation criteria ordered by their contribution to failed calls. Each criterion shows what percentage of all failed audits it was responsible for. This is the coaching prioritisation tool: it tells QA Managers exactly where a targeted intervention will have the greatest measurable impact on the pass rate.

### Business value delivered

- Provides a defensible, documented answer to *"why was this call rated failing?"* — essential for regulatory audit trails and agent appeals
- Identifies whether failures are systemic (whole team) or individual (one agent), enabling the right intervention
- Builds team trust in the AI by making its decision logic transparent and inspectable
- Reduces time to identify the single highest-impact coaching intervention from manual analysis taking hours to a dashboard view taking minutes

---

## 5. Decision Assurance — Can We Trust the AI's Decisions?

**Business objective:** Give QA leadership and Compliance Officers confidence that the AI is making *good quality* decisions, not just *any* decisions — and surface the specific areas where confidence should be lower.

This section is for leaders who need to answer: *"Are we comfortable putting the AI's assessments into performance records, compliance reports, and coaching plans?"*

### Decision quality — are AI scores reliable or inconsistent?

For each evaluation criterion, the platform calculates a **Decision Confidence Score** that combines two factors: how well the criterion is typically scored (the quality), and how consistently it is scored across different calls and evaluators (the reliability).

A criterion that is scored well on average but very inconsistently — sometimes perfect, sometimes zero — is a reliability risk. A high average score that is driven by a few outliers is not the same as a consistently high score. The platform distinguishes these two cases and flags criteria where reliability is low even if the average looks acceptable.

**Business meaning:** Criteria with low confidence scores are not suitable for high-stakes decisions like formal performance reviews. The platform surfaces these specifically, so QA leadership knows which parts of the scorecard to treat with more caution until the underlying inconsistency is resolved.

### Creator impact — which agents are improving and which are at risk?

This view tracks every agent's quality score over two consecutive 30-day periods and calculates a momentum score — whether their performance is improving, stable, or declining.

**Business meaning:** This is the platform's proactive coaching trigger. An agent with declining scores and a high risk classification should receive a coaching conversation *before* their performance causes customer impact or a compliance breach. This replaces the reactive approach of identifying problem agents only after a monthly report — by which time the issue may have affected hundreds of calls.

The view also enables positive recognition: agents whose momentum is strongly positive can be acknowledged and held up as models for their peers.

### Calibration — are we evaluating consistently?

This addresses one of the most common quality assurance problems in contact centres: different QA analysts, or the AI itself, scoring identical agent behaviours very differently. If Agent A and Agent B perform the same action on a call, they should receive the same score regardless of who is reviewing the call.

The platform provides two calibration tools:

**Section calibration.** For each section of the evaluation form, the platform measures scoring consistency — how much variation exists in how that section is scored across different calls and reviewers. A high confusion score on a section means that the scoring guidance for that section is unclear or applied inconsistently. The business action is a calibration session: bring QA analysts together, review scored examples, and align on the standard.

**Agent calibration heatmap.** A grid showing, for each evaluation criterion, how different agents score it. If the same criterion is consistently scored at 90% for calls reviewed by one evaluator and 40% for calls reviewed by another, this is a calibration gap — not a quality gap. Identifying and resolving these gaps ensures that agent performance assessments are fair and defensible.

**Business meaning:** Calibration problems undermine the credibility of the QA programme. If agents or their managers lose trust in the fairness of evaluations, adoption collapses and the business case for the platform fails. The calibration view makes inconsistency visible and actionable before it damages trust.

### Risk Radar — the early warning system

**Business objective:** Provide QA leadership with a single, prioritised list of the highest-risk issues in the QA programme — across all agents, criteria, and call types — so that the weekly governance meeting can focus on the most urgent items without manually reviewing every chart.

The Risk Radar scans all available quality data and surfaces items that match one of four specific risk patterns, each scored from 0 to 100:

---

**Policy Confusion** *(colour: cyan)*

**What it signals:** A specific evaluation criterion is being scored very differently from call to call — the same agent behaviour receives very different marks depending on who reviews it or which call it appears in. The score is not reliably distinguishing good from bad performance.

**Business meaning:** The scoring guidance for this criterion is ambiguous. QA analysts — and the AI — are interpreting the policy differently. This makes the criterion unreliable as a performance measure. It may also create legal risk if a failing score on this criterion is used to justify a performance action and the agent challenges the consistency of the assessment.

**Business action:** Review and rewrite the criterion's definition and scoring guidance. Run a calibration session with QA analysts. Update the AI's knowledge base with clearer policy documentation.

---

**Escalation Risk** *(colour: red)*

**What it signals:** An evaluation criterion that is closely associated with call escalations — for example, "Offered to resolve before escalating" or "Followed de-escalation process" — is being consistently missed or failed across the call population.

**Business meaning:** Agents are not performing the specific behaviour that prevents calls from being escalated. This is both a quality failure and a cost driver: escalated calls consume significantly more agent time and often result in worse customer outcomes. A high Escalation Risk score on a criterion means the problem is widespread, not isolated.

**Business action:** Immediately prioritise coaching on this criterion. Consider increasing the sampling rate for call types where escalation risk is highest so that more calls in this category receive AI and human review. Elevate the criterion's weighting in the evaluation form to make its importance explicit to agents.

---

**Decision Reversal** *(colour: amber)*

**What it signals:** For a specific evaluation criterion, human QA analysts are frequently overriding or partially adjusting the AI's suggested score. The AI is not reaching the same conclusion as the human reviewer.

**Business meaning:** The AI's understanding of what "good" looks like for this criterion does not match the QA team's professional judgement. Until this is resolved, scores on this criterion cannot be used confidently in performance records or coaching plans, because they depend too heavily on human override to be reliable.

**Business action:** Review the AI's reasoning for recent calls on this criterion. Identify whether the disconnect is caused by vague policy documents, an insufficient number of example calls in the knowledge base, or a gap in how the criterion is worded. Update the AI's knowledge base and policy guidance. Re-run calibration checks after the update to verify improvement.

---

**Bias Indicator** *(colour: purple)*

**What it signals:** The AI's score and the human analyst's score consistently diverge in the same direction for a specific criterion or agent group — the AI is systematically higher than the human, or systematically lower.

**Business meaning:** A systematic, directional gap between AI and human scoring is different from random disagreement. It suggests either that the AI has learned a bias — for example, rewarding formal language even when the required behaviour was absent — or that a human analyst has a personal scoring bias that consistently diverges from the team standard. Either way, the criterion cannot be trusted for performance or compliance decisions until the bias is identified and corrected.

**Business action:** For AI bias: review and refine the AI's scoring rubric and knowledge base. Introduce regression tests using a set of "golden" example calls with pre-agreed correct scores. For analyst bias: discuss the specific calls during a calibration session and re-align the analyst with the team standard.

---

**Risk severity levels:**

| Score range | Severity | Expected response |
|---|---|---|
| 70–100 | Urgent (red) | Immediate action; owner assigned before end of week |
| 40–69 | Moderate (amber) | Scheduled for next sprint; tracked in backlog |
| Below 40 | Low (grey) | Monitor; no immediate action required |

**The Risk Radar as a governance tool.** In the weekly TechM GVO review meeting, the Risk Radar is a standing agenda item. It aggregates signals from every other analytics view into a single ranked list, ensuring that the most urgent quality risks receive attention without the meeting needing to step through every individual chart. Red items require an owner and a resolution date before the meeting closes.

### Business value delivered

- Provides evidence that the AI is making trustworthy, consistent decisions — or clearly identifies where it is not
- Enables proactive agent coaching before performance declines cause customer or regulatory impact
- Identifies calibration problems before they undermine team trust in the QA programme
- Provides a governance-ready risk log that can be presented to compliance teams or auditors
- Reduces the time senior QA leaders spend manually identifying the highest-priority issues

---

## 6. AI vs Human Score Comparison — Are the AI's Scores Good Enough to Trust?

**Business objective:** Give QA leadership a measurable, ongoing answer to the question: *"Should we trust the AI's scores?"*

This is the platform's core quality-of-AI measure. The target is that the AI's scores should correlate with human analyst scores at 85% or above — meaning that when a human reviews an AI-scored call, they agree with the AI on 85% or more of the scoring decisions without making substantial changes.

### Section-level comparison

A side-by-side view showing, for each section of the evaluation form, the average score the AI assigned versus the average score the human analyst assigned. Sections where the AI and human consistently differ by more than 10 percentage points are identified as areas where the AI needs improvement.

**Business meaning:** If the AI consistently over-scores the *Compliance* section by 15% compared to human analysts, every call evaluated by the AI alone is likely to have an inflated compliance score. This is a known, measurable risk that can be corrected by improving the AI's policy guidance for that section.

### Parameter-level comparison

A detailed breakdown showing, for every individual evaluation criterion, how much the AI and human scores differed on average. This can be sorted to bring the biggest gaps to the top.

**Business meaning:** This table is the input to the AI improvement roadmap. The criteria with the largest consistent gap between AI and human scores are the specific items where the AI's understanding needs to be improved through better knowledge-base content or clearer scoring guidance. Each sprint, the team can target the top two or three gaps, track whether the improvement closes the gap, and progressively increase overall AI reliability towards and beyond the 85% correlation target.

### Business value delivered

- Provides an ongoing, measurable confidence indicator for the AI system
- Identifies specific criteria where AI improvement will have the greatest impact on reliability
- Supports the business case for continued investment: as the correlation score rises towards and beyond 85%, the proportion of human review time that can be reduced increases
- Creates a documented improvement trajectory suitable for presenting to governance or assurance stakeholders

---

## 7. Closing the Loop — Connection to Training Needs Identification

The analytics and explainability platform does not operate in isolation. Every insight it surfaces connects directly to the **Training Needs Identification (TNI)** module, which translates quality data into agent coaching plans.

When the Call Pipeline completes a batch of evaluations and an agent's scores fall below the quality threshold, the platform automatically generates a Training Needs Identification plan for that agent, listing the specific criteria they missed and recommending targeted training resources.

This means the business workflow is:

1. **Analytics Dashboard** identifies that agent scores are declining on *Compliance Disclosure*
2. **Explainability Analytics** confirms that *Compliance Disclosure* is the top contributor to failed calls
3. **Decision Assurance → Creator Impact** confirms which specific agents are declining on this criterion
4. **TNI** automatically creates personalised coaching plans for those agents, citing the specific calls and criteria
5. **Decision Assurance → Calibration** confirms that after coaching, scores on the criterion become more consistent

This end-to-end loop — from analytics insight to coaching action to measurable improvement — is the core business value proposition of the platform.

---

## 8. Summary — Business Value of the Analytics Suite

| Feature | Who benefits | Business problem solved | Measurable outcome |
|---|---|---|---|
| Analytics Dashboard | QA Manager, Operations Director | Cannot see quality trends without manual report compilation | Weekly management report replaced by live dashboard; no export required |
| Explainability Analytics | Compliance Officer, QA Manager | Cannot explain why a call was rated failing | Full audit trail of why every call was scored as it was |
| Decision Drivers | QA Team Leader | Cannot identify which training will have most impact on pass rates | Coaching prioritised by pass-rate impact, not by guesswork |
| HITL Agreement | QA Manager, Compliance | Cannot demonstrate meaningful human oversight of AI decisions | Documented agree/disagree rate by call category, per review period |
| Decision Assurance — Quality | QA Manager, Compliance | Cannot tell whether AI scores are reliable or inconsistent | Per-criterion confidence score; low-confidence criteria flagged |
| Creator Impact | QA Team Leader | Coaching is reactive (after monthly report) not proactive | Declining agents identified and coached before performance causes impact |
| Calibration + Heatmap | QA Manager | Inconsistent scoring across analysts creates unfair evaluations | Calibration gaps made visible; sessions targeted at highest-spread criteria |
| Risk Radar | Operations Director, TechM GVO | No single view of the highest-priority QA risks across all dimensions | Weekly governance meeting runs from a single ranked risk list |
| AI vs Human Comparison | QA Manager, Product Owner | Cannot tell whether AI quality is improving or declining over time | Measurable AI-human correlation score, tracked per sprint |
| TNI Linkage | QA Team Leader, Agent | Coaching plans created manually, often generic | Personalised, criteria-specific coaching plans generated automatically |

---

*This document describes the business requirements and value delivered by the Analytics & Explainability features of the TechM AI QA Automation Platform. For technical implementation details, see `TechM-AI-QA-Design-Doc-Descriptive-Copy.md` (Section 2: Analytics & Explainability Platform) and `Outcomes-Measurement-Analytics-Explainability.txt`.*
