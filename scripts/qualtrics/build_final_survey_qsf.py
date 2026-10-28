#!/usr/bin/env python3
"""Build the side-by-side final survey (Selected Voice | Self-Similar Voice) as a Qualtrics QSF.

Item wording is read from the thesis instrument appendix (docs/thesis/appendices/a-instruments.tex)
so that the survey and the thesis cannot drift apart. The QSF structure follows a side-by-side
question exported from UCF Qualtrics (template stored next to the output).

Usage: build_final_survey_qsf.py [--template TEMPLATE.qsf] [--out OUT.qsf]
Writes the QSF and an item map CSV (export tag -> section, row, wording, scale).
"""
import argparse
import copy
import csv
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APPENDIX = ROOT / "docs/thesis/appendices/a-instruments.tex"
OUTDIR = ROOT / "resources/surveys/qualtrics"
COLUMNS = ["Selected Voice", "Self-Similar Voice"]
# Set from the survey URL query string by the study runtime (final-survey-handoff JSON).
EMBEDDED_FIELDS = ["participant_id", "session_id", "run_id", "condition_order", "selected_voice_block",
                   "self_similar_voice_block", "selected_voice_name", "handoff_id"]


def clean(tex: str) -> str:
    t = tex.replace("``", "\u201c").replace("''", "\u201d").replace("--", "\u2013")
    t = re.sub(r"\\href\{([^}]*)\}\{([^}]*)\}", r'<a href="\1" target="_blank">\2</a>', t)
    t = re.sub(r"\s*~?\\cite\{[^}]*\}", "", t)
    t = re.sub(r"\s*\\textit\{\([^}]*\)\}", "", t)  # drop dimension tags such as (Helpfulness)
    t = re.sub(r"\\textit\{([^}]*)\}", r"\1", t)
    t = re.sub(r"\\emph\{([^}]*)\}", r"\1", t)
    t = t.replace("\\%", "%").replace("~", " ").replace("\\&", "&")
    return re.sub(r"\s+", " ", t).strip()


def parse_final_survey():
    s = APPENDIX.read_text()
    a = s.index("\\section{Final survey: side-by-side voice ratings}")
    b = s.index("\\section{Final survey: comparison and debrief}")
    sec = s[a:b]
    sections = []
    for sub in re.split(r"(?=\\subsection\{)", sec)[1:]:
        title = re.match(r"\\subsection\{([^}]*)\}", sub).group(1)
        intro = clean(sub.split("\n", 2)[1]) if "\n" in sub else ""
        groups, current = [], None
        for chunk in re.split(r"(?=\\subsubsection\*\{)", sub):
            m = re.match(r"\\subsubsection\*\{([^}]*)\}", chunk)
            name = m.group(1) if m else ""
            items = []
            for it in re.finditer(r"\\item(?:\[([^\]]*)\])?\s*\\begin\{minipage\}\[t\]\{\\linewidth\}\s*(.*?)\\survey(seven|tlx)pair\{([^}]*)\}\{([^}]*)\}(?:\{([^}]*)\})?", chunk, re.S):
                label, text, kind, a1, a2, a3 = it.groups()
                items.append({"label": (label or "").rstrip("."), "text": clean(text), "kind": kind,
                              "anchors": (a1, a2, a3 or "") if kind == "seven" else (a1, a2)})
            if items:
                groups.append({"name": name, "items": items})
        sections.append({"title": title, "intro": intro, "groups": groups, "raw": sub})
    return sections


class Builder:
    def __init__(self, template):
        self.t = template
        self.sid = template["SurveyEntry"]["SurveyID"]
        self.questions, self.blocks, self.n = [], [], 0
        self.rows = []

    def qid(self):
        self.n += 1
        return f"QID{self.n}"

    def _base(self, qid, tag, text, qtype, selector, sub=None):
        p = {"QuestionText": text, "DefaultChoices": False, "DataExportTag": tag, "QuestionID": qid,
             "QuestionType": qtype, "Selector": selector, "DataVisibility": {"Private": False, "Hidden": False},
             "Configuration": {"QuestionDescriptionOption": "UseText"},
             "QuestionDescription": re.sub(r"<[^>]+>", " ", text)[:200],
             "Validation": {"Settings": {"ForceResponse": "OFF", "Type": "None"}}, "Language": []}
        if sub:
            p["SubSelector"] = sub
        return p

    def add(self, payload):
        self.questions.append({"SurveyID": self.sid, "Element": "SQ", "PrimaryAttribute": payload["QuestionID"],
                               "SecondaryAttribute": payload["QuestionDescription"][:100], "TertiaryAttribute": None,
                               "Payload": payload})
        self.blocks[-1]["BlockElements"].append({"Type": "Question", "QuestionID": payload["QuestionID"]})

    def block(self, name):
        self.blocks.append({"Type": "Standard", "SubType": "", "Description": name,
                            "ID": f"BL_fmv{len(self.blocks)+1:011d}", "BlockElements": []})

    def page_break(self):
        self.blocks[-1]["BlockElements"].append({"Type": "Page Break"})

    def db(self, tag, text):
        p = self._base(self.qid(), tag, text, "DB", "TB")
        p["ChoiceOrder"] = []
        self.add(p)

    def te(self, tag, text, essay=False):
        self.add(self._base(self.qid(), tag, text, "TE", "ESTB" if essay else "SL"))

    def mc(self, tag, text, choices, multiple=False):
        p = self._base(self.qid(), tag, text, "MC", "MAVR" if multiple else "SAVR", "TX")
        p["Choices"] = {str(i + 1): {"Display": c} for i, c in enumerate(choices)}
        p["ChoiceOrder"] = [str(i + 1) for i in range(len(choices))]
        p["NextChoiceId"] = len(choices) + 1
        p["NextAnswerId"] = 1
        self.add(p)

    def sbs(self, tag, text, statements, answers, section):
        qid = self.qid()
        p = self._base(qid, tag, text, "SBS", "SBSMatrix")
        p["Configuration"]["RepeatHeaders"] = "none"
        ch = {str(i + 1): {"Display": st} for i, st in enumerate(statements)}
        order = [str(i + 1) for i in range(len(statements))]
        p.update({"Choices": ch, "ChoiceOrder": order, "GradingData": [], "NextChoiceId": len(statements) + 1,
                  "NextAnswerId": len(answers) + 1, "NumberOfQuestions": len(COLUMNS), "ChoiceDataExportTags": False})
        add = {}
        for c, col in enumerate(COLUMNS, start=1):
            add[str(c)] = {"Answers": {str(i + 1): {"Display": a} for i, a in enumerate(answers)},
                           "AnswerOrder": [str(i + 1) for i in range(len(answers))],
                           "QuestionText": col, "RecodeValues": [], "VariableNaming": [], "AnalyzeChoices": [],
                           "QuestionType": "Matrix", "Selector": "Likert", "SubSelector": "SingleAnswer",
                           "ChoiceColumnWidthPixels": None, "DataExportTag": f"{tag}#{c}", "QuestionID": f"{qid}#{c}",
                           "Choices": copy.deepcopy(ch), "ChoiceDataExportTags": False, "QuestionDescription": col,
                           "Configuration": {"QuestionDescriptionOption": "UseText"}}
        p["AdditionalQuestions"] = add
        self.add(p)
        for i, st in enumerate(statements, start=1):
            self.rows.append({"export_tag": tag, "row": i, "section": section, "wording": st,
                              "columns": " | ".join(f"{tag}#{c}_{i}" for c in range(1, len(COLUMNS) + 1)),
                              "scale": f"{answers[0]} .. {answers[-1]}"})

    def qsf(self):
        out = copy.deepcopy(self.t)
        out["SurveyEntry"]["SurveyName"] = "Follow My Voice \u2014 Final Survey (Side-by-Side, ARQ) v2026-10-28c DRAFT"
        els = [e for e in out["SurveyElements"] if e["Element"] not in ("SQ", "BL", "FL", "QC")]
        trash = {"Type": "Trash", "Description": "Trash / Unused Questions", "ID": "BL_fmvtrash000001"}
        els.append({"SurveyID": self.sid, "Element": "BL", "PrimaryAttribute": "Survey Blocks",
                    "SecondaryAttribute": None, "TertiaryAttribute": None, "Payload": self.blocks + [trash]})
        ed = [{"Description": f, "Type": "Recipient", "Field": f, "VariableType": "String", "DataVisibility": [], "AnalyzeText": False}
              for f in EMBEDDED_FIELDS]
        flow = [{"Type": "EmbeddedData", "FlowID": "FL_2", "EmbeddedData": ed}]
        flow += [{"ID": b["ID"], "Type": "Block", "FlowID": f"FL_{i+3}"} for i, b in enumerate(self.blocks)]
        els.append({"SurveyID": self.sid, "Element": "FL", "PrimaryAttribute": "Survey Flow", "SecondaryAttribute": None,
                    "TertiaryAttribute": None, "Payload": {"Flow": flow, "Properties": {"Count": len(flow) + 1},
                                                           "FlowID": "FL_1", "Type": "Root"}})
        els.append({"SurveyID": self.sid, "Element": "QC", "PrimaryAttribute": "Survey Question Count",
                    "SecondaryAttribute": str(len(self.questions)), "TertiaryAttribute": None, "Payload": None})
        out["SurveyElements"] = els + self.questions
        return out


def scale_answers(item):
    if item["kind"] == "tlx":
        lo, hi = item["anchors"]
        return [f"{i}" for i in range(11)], lo, hi
    lo, mid, hi = item["anchors"]
    return [f"{i}" for i in range(1, 8)], lo, hi


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--template", default=str(OUTDIR / "final-survey-sbs-template.qsf"))
    ap.add_argument("--out", default=str(OUTDIR / "04-final-side-by-side.qsf"))
    a = ap.parse_args()
    template = json.loads(Path(a.template).read_text())
    b = Builder(template)
    cols = "the <b>Selected Voice</b> (the voice you chose) and the <b>Self-Similar Voice</b> (the synthetic version of your own voice)"

    b.block("Study information")
    b.db("FINAL_INTRO", "DRAFT \u2014 Follow My Voice final survey. Complete after both voice blocks. For each statement, "
         f"rate {cols} side by side, thinking about the block in which you heard each voice. There are no right or wrong "
         "answers, and you may leave an item unanswered if you cannot answer it.")
    b.te("FINAL_STUDY_CODE", "Study code (researcher entry):")
    b.te("FINAL_ORDER_CODE", "Condition order code (researcher entry):")

    for sec in parse_final_survey():
        title = sec["title"]
        if not sec["groups"]:
            continue
        b.block(title)
        for gi, g in enumerate(sec["groups"]):
            items = g["items"]
            answers, lo, hi = scale_answers(items[0])
            answers = list(answers)
            answers[0] = lo if re.match(r"^\d+\s*=", lo) else f"{answers[0]}<br>{lo}"
            answers[-1] = hi if re.match(r"^\d+\s*=", hi) else f"{answers[-1]}<br>{hi}"
            tagbase = re.sub(r"[^A-Z0-9]+", "_", (title + (" " + g["name"] if g["name"] else "")).upper()).strip("_")[:40]
            labels = [it["label"] for it in items]
            statements = [(f"{it['label']}. " if it["label"] else "") + it["text"] for it in items]
            head = sec["intro"] if gi == 0 else ""
            # participant-facing text: drop researcher-facing provenance sentences
            head = " ".join(x for x in re.split(r"(?<=[.;])\s+", head) if not re.search(r"et al|adapts|draft item", x))
            # construct names stay in block names and export tags, not in participant-facing text
            qtext = head or "Rate each statement for each voice, using the same scale as above."
            b.sbs(tagbase, qtext, statements, answers, f"{title}{' / ' + g['name'] if g['name'] else ''}")
        if "Voice and assistance" in title:
            for c in COLUMNS:
                key = "SEL" if c.startswith("Selected") else "SELF"
                b.te(f"LIKE_{key}", f"How much did you like the <b>{c}</b>? (0 = Do not like at all, 100 = Like extremely; leave blank if unable to rate)")
            for c in COLUMNS:
                key = "SEL" if c.startswith("Selected") else "SELF"
                b.te(f"RELY_{key}", f"For the <b>{c}</b>, what percentage of the time did you use the agent's guidance to decide where to search? (0\u2013100%; leave blank if unable to estimate)")
            for c in COLUMNS:
                key = "SEL" if c.startswith("Selected") else "SELF"
                b.te(f"GENDER_{key}", f"How would you describe the gender presentation of the <b>{c}</b>? (optional)")
        b.page_break()

    b.block("Technical and comfort checks")
    b.sbs("CHECKS", "Answer each question for each voice.",
          ["Did you hear all prompts clearly?",
           "Did any prompt unexpectedly repeat, cut off, arrive late, or use the wrong voice?",
           "Did you take an unplanned break or experience an interruption?"],
          ["Yes", "No", "Unsure"], "Technical and comfort checks")
    b.te("CHECKS_DETAIL", "If you answered Yes to a problem above, please describe it (optional).", essay=True)
    b.te("CHECKS_OTHER", "Is there anything else you would like to share about either voice? (optional)", essay=True)

    b.block("Demographics")
    b.mc("AGE_RANGE", "Age range:", ["18\u201324", "25\u201334", "35\u201344", "45\u201354", "55\u201364", "65+", "Prefer not to answer"])
    b.mc("GENDER", "Gender identity (select all that apply):", ["Woman", "Man", "Nonbinary", "Another identity", "Prefer not to answer"], multiple=True)
    b.te("GENDER_DESC", "If you selected another identity, you may describe it here (optional).")

    b.block("Voice comparison and debrief")
    b.mc("PREF", "Which voice did you prefer?", ["The Selected Voice", "The Self-Similar Voice", "No preference", "Unsure"])
    s = APPENDIX.read_text()
    deb = s[s.index("\\section{Final survey: comparison and debrief}"):s.index("\\section{Fallback Voice-Recording Passage}")]
    qs = re.findall(r"\\item\s+(.*?)\n", deb[deb.index("\\begin{enumerate}"):deb.index("\\end{enumerate}")])
    for i, q in enumerate(qs, start=1):
        b.te(f"DEBRIEF_{i:02d}", clean(q) + " (researcher notes or participant response; optional)", essay=True)

    out = Path(a.out)
    out.write_text(json.dumps(b.qsf(), ensure_ascii=False))
    with open(out.with_suffix(".item-map.csv"), "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["export_tag", "row", "section", "wording", "columns", "scale"])
        w.writeheader()
        w.writerows(b.rows)
    print(f"{out}: {len(b.questions)} questions, {len(b.rows)} side-by-side rows, {len(b.blocks)} blocks")


if __name__ == "__main__":
    main()
