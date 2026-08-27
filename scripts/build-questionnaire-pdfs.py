#!/usr/bin/env python3
"""Generate split, UCF-themed per-instrument questionnaire PDFs from questionnaires.md.

Requires: python-docx, weasyprint, and pandoc on PATH. Outputs into
docs/irb/submission/. Run after editing docs/irb/source/questionnaires.md.
"""
from __future__ import annotations
import re, subprocess, base64, zipfile, io, os
from pathlib import Path
from weasyprint import HTML

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "docs/irb/source/questionnaires.md"
TPL = ROOT / "docs/irb/templates/HRP-503 - TEMPLATE - Protocol.docx"
OUT = ROOT / "docs/irb/submission"

def ucf_logo_b64() -> str:
    with zipfile.ZipFile(TPL) as z:
        return base64.b64encode(z.read("word/media/image1.png")).decode()

def sections(md: str) -> dict[str, str]:
    return {p.splitlines()[0].strip(): "## " + p for p in re.split(r"(?m)^## ", md)[1:]}

# paragraph starts that are researcher-facing and should NOT appear on participant forms
DROP_PREFIXES = (
    "Reference", "Reverse-scored items", "Items 1", "Analyze", "Compute ",
    "Score subscales", "Score the", "Administer",
)

def clean_participant(md: str) -> str:
    """Drop researcher-facing paragraphs (citations, scoring/analysis notes)."""
    blocks = re.split(r"\n\s*\n", md)
    kept = [b for b in blocks if not any(b.strip().startswith(p) for p in DROP_PREFIXES)]
    return "\n\n".join(kept)

def add_evengrid(frag: str, idx: list[int]) -> str:
    i = [0]
    def repl(m):
        tag = m.group(0)
        if i[0] in idx:
            tag = '<table class="evengrid">' if "class=" not in tag else tag
        i[0] += 1
        return tag
    return re.sub(r"<table[^>]*>", repl, frag)

CSS = """
@page { size: letter; margin: 0.7in 0.75in; }
* { font-family: Arial, Helvetica, sans-serif; color:#222; }
.hdr { border-bottom:3px solid #FFC904; padding-bottom:8px; margin-bottom:14px; }
.hdr img { height:34px; vertical-align:middle; }
.stitle { color:#1F4E79; font-size:16px; font-weight:bold; margin:2px 0 6px 0; }
.sid { font-size:11px; margin:0 0 14px 0; }
h2 { color:#1F4E79; font-size:13px; margin:14px 0 6px 0; }
p, li, td, th { font-size:10.5px; line-height:1.35; }
table { border-collapse:collapse; width:100%; margin:8px 0 12px 0; }
th, td { border:1px solid #bbb; padding:4px 5px; text-align:center; vertical-align:middle; }
th { background:#f2f2f2; color:#1F4E79; font-weight:bold; }
td:first-child, th:first-child { text-align:left; }
strong { color:#9c0006; }
.evengrid { table-layout:fixed; }
.evengrid td:first-child, .evengrid th:first-child { width:34%; }
.narrowlabel td:first-child, .narrowlabel th:first-child { width:30%; }
"""

def main():
    md = SRC.read_text(encoding="utf-8")
    sec = sections(md)
    logo = ucf_logo_b64()

    # NASA-TLX: drop redundant heading, methodology/citation line, condition-code note
    nasa = sec["Raw NASA Task Load Index (after each block)"]
    nasa = nasa.replace("## Raw NASA Task Load Index (after each block)\n", "")
    nasa = re.sub(r"Please rate your workload during this block\..*?62386-9\)\.\s*", "", nasa, flags=re.S)
    nasa = re.sub(r"These condition-code fields are completed automatically/by the researcher and are not shown as manipulation labels if doing so would bias responses\.\s*", "", nasa)
    nasa = re.sub(r"\\\s*$", "", nasa.rstrip())
    sec["Raw NASA Task Load Index (after each block)"] = nasa

    # Presence (IPQ): drop redundant heading + the methodology/anchor-instruction intro
    if "Presence Questionnaire (post-session)" in sec:
        pres = sec["Presence Questionnaire (post-session)"]
        pres = pres.replace("## Presence Questionnaire (post-session)\n", "")
        pres = re.sub(r"igroup Presence Questionnaire \(IPQ;.*?item-specific anchors\.\]\*\*\s*", "", pres, flags=re.S)
        sec["Presence Questionnaire (post-session)"] = pres

    # Eeriness: drop the citation / anchor-transcription note from the intro sentence
    if "Perceived Eeriness (after each block)" in sec:
        eer = sec["Perceived Eeriness (after each block)"]
        eer = re.sub(r" Adapted from the Ho & MacDorman.*?before participant use\.\]\*\*", "", eer, flags=re.S)
        sec["Perceived Eeriness (after each block)"] = eer

    # Assistance: labeled 7-point agreement matrix
    ANCH = ["Strongly disagree", "Disagree", "Somewhat disagree", "Neutral", "Somewhat agree", "Agree", "Strongly agree"]
    items = re.findall(r"(?m)^\d+\.\s+(.*?)\s+1 2 3 4 5 6 7\s*$", sec["Post-Block Assistance and Voice Check"])
    def matrix(rows):
        head = "<tr><th>Statement</th>" + "".join(f"<th>{a}</th>" for a in ANCH) + "</tr>"
        body = "".join("<tr><td>" + it + "</td>" + "<td>○</td>" * 7 + "</tr>" for it in rows)
        return f'<table class="evengrid narrowlabel"><thead>{head}</thead><tbody>{body}</tbody></table>'
    assist = (
        "<p>Rate each statement from strongly disagree to strongly agree.</p>"
        + matrix(items)
        + "<p><strong>Technical check</strong></p><ul>"
        + "<li>Did you hear all prompts clearly? ☐ Yes ☐ No ☐ Unsure</li>"
        + "<li>Did any prompt repeat, cut off, arrive late, or use the wrong voice? ☐ No ☐ Yes: ______</li>"
        + "<li>Did you take an unplanned break or experience an interruption? ☐ No ☐ Yes: ______</li></ul>"
    )

    GROUPS = [
        ("Demographics and Prior Experience", "01_Demographics_and_Prior_Experience.pdf", ["Background and Prior Experience (once per participant)"], False, [], None),
        ("NASA Task Load Index (NASA-TLX)", "02_NASA-TLX.pdf", ["Raw NASA Task Load Index (after each block)"], True, [0], None),
        ("Assistance and Voice Ratings", "03_Assistance_and_Voice_Ratings.pdf", None, False, [], assist),
        ("Perceived Eeriness and Voice Similarity", "04_Perceived_Eeriness.pdf", ["Perceived Eeriness (after each block)"], True, [1], None),
        ("Post-Session Comparison", "05_Post-Session_Comparison.pdf", ["Post-Session Comparison"], False, [], None),
        ("Simulator Sickness Questionnaire (SSQ)", "06_Simulator_Sickness_Questionnaire.pdf", ["Post-Exposure Simulator Sickness Questionnaire"], True, [0], None),
        ("Presence Questionnaire", "07_Presence.pdf", ["Presence Questionnaire (post-session)"], True, [0], None),
    ]

    for title, fname, sects, circles, even, custom in GROUPS:
        if custom is not None:
            frag = custom
        else:
            body = "\n\n".join(sec[s] for s in sects if s in sec)
            body = clean_participant(body)
            frag = subprocess.run(["pandoc", "-f", "gfm", "-t", "html"], input=body, capture_output=True, text=True).stdout
            if circles:
                frag = frag.replace("☐", "○")
            if even:
                frag = add_evengrid(frag, even)
        doc = (f'<!doctype html><html><head><meta charset="utf-8"><style>{CSS}</style></head><body>'
               f'<div class="hdr"><img src="data:image/png;base64,{logo}"></div>'
               f'<div class="stitle">{title}</div>'
               f'<div class="sid">Study ID: ______________________&nbsp;&nbsp;&nbsp; Use study ID only; do not write your name. You may skip any nonessential question.</div>'
               f'{frag}</body></html>')
        HTML(string=doc, base_url=str(ROOT)).write_pdf(str(OUT / fname))
        print("wrote", fname)

if __name__ == "__main__":
    main()
