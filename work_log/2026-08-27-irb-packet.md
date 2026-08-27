# Work Log — 2026-08-27 — IRB Packet (Follow My Voice)

Branch: `v2` · Huron study: **STUDY00009581** · Study: *Follow My Voice: Gaze-Contingent
XR Search with a Self-Similar Agent*

Summary of the day's IRB work: filled the official UCF templates, split and styled the
questionnaire instruments, genericized wording, and populated the Huron smartform.

## Official UCF templates filled (in-place, preserving native format/tables)
- **HRP-503 Protocol** — filled the blank UCF template by replacing only the red
  instructional text; preserved tables (Version History, Study Summary), headings,
  checkboxes. Later replaced with Pedro's canonically edited version. → `output/irb/HRP-503_Follow_My_Voice_Protocol.docx`
- **HRP-502 Adult Consent** — filled the UCF consent template (lay Q&A), kept the adult
  signature block, removed witness/LAR/assent blocks, added a Voice-Cloning Authorization
  section. → `output/irb/HRP-502_Follow_My_Voice_Consent.docx`

## Administrative fields resolved
- PI **Dr. Roshan Venkatakrishnan** (roshan.venkatakrishnan@ucf.edu, 407-823-3957,
  Computer Science / CECS); Student investigator **Pedro Poveda** (pe011248@ucf.edu).
- Rooms **HEC 208 / HEC 308 / BYC 119**; funding = **approved lab funds** ($15 gift card);
  submission date 2026-08-18. Reused institutional facts from the **Study 9491** lab
  reference packet (same PI).

## Content edits (kept intentionally general to minimize future amendments)
- Removed the **DRAFT** banner and the **`_DRAFT`** filename suffix from the generated packet.
- Removed all **"swivel chair"** references (kept "seated" + objects arranged around the participant).
- Genericized the headset: **"HTC Vive Focus Vision" → "mixed-reality head-mounted display"**
  across source docs and the filled HRP-502/503.

## Questionnaires
- Added a validated **Perceived Eeriness** semantic-differential matrix (Ho & MacDorman 2017;
  Zibrek et al. 2018; Guo et al. 2024 precedent) + a **voice-similarity** manipulation check.
  Added the two eeriness citations to `references.bib`.
- Confirmed **NASA-TLX at 0–10** (matrix form; discretized raw TLX, matching the lab's prior
  approved study) — synced in the decision register.
- Split the questionnaires into **6 UCF-themed PDFs** (`docs/irb/submission/0X_*.pdf`):
  Demographics, NASA-TLX, Assistance & Voice Ratings, Perceived Eeriness, Post-Session
  Comparison, SSQ. Used the real UCF logo from the packet template.
- PDF formatting iterations: removed duplicate UCF wordmark text; removed study-title
  subtitle; trimmed the NASA-TLX PDF (redundant heading, citation line, condition-code note);
  evenly spaced rating-matrix bubbles (fixed column widths); reformatted the assistance items
  into a **labeled 7-point agreement matrix** (Strongly disagree → Strongly agree).

## Tooling / fixes
- Fixed `build-irb-packet.sh` so it **preserves `*.pdf` files across rebuilds** (the rebuild
  had wiped the split questionnaire PDFs — they were recovered from git; the build now copies
  PDFs from the backup back into `submission/`).

## Huron smartform (STUDY00009581)
- Basic Study Information: confirmed (title/short title/description already entered).
- Study Funding Sources: leave empty (unfunded; lab funds are internal, not a funding source).
- Local Study Team Members: Roshan (Faculty Advisor) + Pedro; do **not** add thesis committee.
- Local Site Documents: upload the filled consent/protocol (from `output/irb/`) and the
  bespoke docs + 6 questionnaire PDFs (from `docs/irb/submission/`) via **Update** on each row.
  Note: file uploads must be done manually — Huron's "Choose File" opens the native macOS
  picker, which browser automation can't operate.

## Still open
- **Eeriness anchors**: swap the placeholder pairs (Reassuring–Eerie, Natural–Uncanny,
  Comforting–Creepy) for the exact validated Ho & MacDorman / Guo wording before use.
- UCF determinations (risk level, injury language, clinical-trial classification, CoC) and
  the simulation-based power analysis remain preregistration/submission gates.
- CITI training: passed 3 UCF Stage 1 Basic Courses (IRB Protocol Review; Research and HIPAA
  Privacy Protections; Researchers – Information Privacy & Security) — logged 2026-08-18.
