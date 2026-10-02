# CamGuard Workflow Training Diagram

## Objective
Create a training-ready architecture and workflow diagram that explains how a camera image flows through CamGuard and how the key decisions affect identity checks, access outcomes, notifications, and file handling.

## Problem and Why
CamGuard has separate Enrollment and Monitoring modules, and the monitoring workflow includes important short-circuit and fallback decisions. A diagram will let trainees follow these relationships without reconstructing them from source code.

## Scope and Constraints
- Add one English Markdown training artifact under `docs/architecture/`.
- Include a conceptual ER-style relationship map and a decision flowchart grounded in current source behavior.
- Keep storage technology generic where the inspected code does not establish a concrete schema or adapter.
- This is documentation only; no application behavior changes.

## Authorized Scope
- `docs/architecture/camguard-workflow-training.md`
- `odd/tasks/camguard-workflow-training.md`

## Delivery Strategy
- `ask-on-risk` (default); forecast approximately 130 authored changed lines.

## Acceptance Criteria
- A reader can identify the Enrollment and Monitoring responsibilities and their boundary.
- The flowchart shows the zero-face short circuit, identity match and access decision, notification routing, and processed/quarantine outcomes.
- The diagrams distinguish observed application behavior from conceptual entity relationships.
- The Markdown and Mermaid fences are structurally readable on readback.

## Tasks
- [x] T1 — Create the training diagram and perform structural readback.
  - Route: direct inline; one documentation artifact with a bounded, source-grounded scope.
  - Checks: inspect the completed Markdown and verify all described branches against the explored source.
  - Evidence: structural readback checked the enrollment boundary, zero-face short circuit, null-distance denial, policy outcomes, notification fallbacks, and archive/quarantine behavior against CodeGraph source results. An independent read-only review returned PASS and identified one clarity improvement; the component map now shows the direct zero-face edge. Mermaid rendering was not checked. Three Mermaid diagrams are present: component map, conceptual relationship map, and end-to-end decisions. Work-unit commit: `75b5517` (`docs(architecture): add CamGuard training diagrams`).

## Progress and Next Step
The training artifact is complete and committed on `docs/camguard-workflow-training`. No application code changed; no test or build was applicable. RDD status is off (clone-local setting); its review assessment returned high/unassessable because untracked files need an explicit inventory, and also reported an untrusted `.git` ownership path. Native review was not started because RDD is off. Next: none.
