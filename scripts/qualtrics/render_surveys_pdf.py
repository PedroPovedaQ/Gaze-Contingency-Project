"""Render the Qualtrics Advanced TXT drafts as print-ready review PDFs.

These are static review copies of the current Qualtrics drafts. They are meant
for protocol review and experimenter printing, not as a replacement for the
Qualtrics response forms.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass, field
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import (
    KeepTogether,
    PageBreak,
    Paragraph,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "resources" / "surveys" / "qualtrics"
OUTPUT_DIR = ROOT / "output" / "pdf"


@dataclass
class Question:
    kind: str
    question_id: str = ""
    prompt: str = ""
    choices: list[str] = field(default_factory=list)
    answers: list[str] = field(default_factory=list)


@dataclass
class Block:
    name: str
    items: list[Question | str] = field(default_factory=list)


def ascii_text(value: str) -> str:
    """Normalize imported survey punctuation for broad PDF font support."""

    replacements = {
        "\u2010": "-",
        "\u2011": "-",
        "\u2012": "-",
        "\u2013": "-",
        "\u2014": "-",
        "\u2212": "-",
        "\u2018": "'",
        "\u2019": "'",
        "\u201c": '"',
        "\u201d": '"',
        "\u00a0": " ",
        "\u2026": "...",
    }
    value = "".join(replacements.get(char, char) for char in value)
    return unicodedata.normalize("NFKC", value)


def parse_advanced_txt(path: Path) -> list[Block]:
    blocks: list[Block] = []
    current_block: Block | None = None
    current_question: Question | None = None
    mode: str | None = None

    def flush_question() -> None:
        nonlocal current_question
        if current_question is not None and current_block is not None:
            current_block.items.append(current_question)
        current_question = None

    def ensure_block() -> Block:
        nonlocal current_block
        if current_block is None:
            current_block = Block("Survey content")
            blocks.append(current_block)
        return current_block

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line == "[[AdvancedFormat]]":
            continue
        match = re.fullmatch(r"\[\[Block:(.+)\]\]", line)
        if match:
            flush_question()
            current_block = Block(ascii_text(match.group(1)))
            blocks.append(current_block)
            mode = None
            continue
        if line == "[[PageBreak]]":
            flush_question()
            ensure_block().items.append("pagebreak")
            mode = None
            continue
        match = re.fullmatch(r"\[\[Question:(.+)\]\]", line)
        if match:
            flush_question()
            kind = match.group(1)
            current_question = Question(kind=kind)
            mode = "prompt"
            continue
        match = re.fullmatch(r"\[\[ID:(.+)\]\]", line)
        if match and current_question is not None:
            current_question.question_id = ascii_text(match.group(1))
            mode = "prompt"
            continue
        if line == "[[Choices]]" and current_question is not None:
            mode = "choices"
            continue
        if line == "[[AdvancedAnswers]]" and current_question is not None:
            mode = "answers"
            continue
        if re.fullmatch(r"\[\[Answer:\d+\]\]", line):
            mode = "answers"
            continue
        if current_question is None:
            continue
        if mode == "choices":
            current_question.choices.append(ascii_text(line))
        elif mode == "answers":
            current_question.answers.append(ascii_text(line))
        else:
            current_question.prompt = ascii_text(line)
    flush_question()
    return blocks


def styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "SurveyTitle", parent=base["Title"], fontName="Helvetica-Bold",
            fontSize=20, leading=24, alignment=TA_CENTER, textColor=colors.HexColor("#15324B"),
            spaceAfter=8,
        ),
        "subtitle": ParagraphStyle(
            "SurveySubtitle", parent=base["Normal"], fontName="Helvetica",
            fontSize=9, leading=12, alignment=TA_CENTER, textColor=colors.HexColor("#566573"),
            spaceAfter=18,
        ),
        "block": ParagraphStyle(
            "BlockHeading", parent=base["Heading1"], fontName="Helvetica-Bold",
            fontSize=15, leading=18, textColor=colors.HexColor("#15324B"),
            spaceBefore=6, spaceAfter=10,
        ),
        "question": ParagraphStyle(
            "Question", parent=base["Normal"], fontName="Helvetica-Bold",
            fontSize=10.5, leading=14, textColor=colors.HexColor("#1D2D3A"),
            spaceBefore=5, spaceAfter=4,
        ),
        "body": ParagraphStyle(
            "Body", parent=base["Normal"], fontName="Helvetica",
            fontSize=9.3, leading=12.5, textColor=colors.HexColor("#263746"),
            spaceAfter=4,
        ),
        "small": ParagraphStyle(
            "Small", parent=base["Normal"], fontName="Helvetica",
            fontSize=8, leading=10, textColor=colors.HexColor("#566573"),
        ),
        "callout": ParagraphStyle(
            "Callout", parent=base["Normal"], fontName="Helvetica",
            fontSize=9.3, leading=13, textColor=colors.HexColor("#263746"),
        ),
    }


def p(text: str, style: ParagraphStyle) -> Paragraph:
    safe = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return Paragraph(safe, style)


def meta_table(metadata: list[tuple[str, str]], st: dict[str, ParagraphStyle]) -> Table:
    rows = [[p(label, st["small"]), p(value, st["body"])] for label, value in metadata]
    table = Table(rows, colWidths=[1.35 * inch, 5.95 * inch], hAlign="CENTER")
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (0, -1), colors.HexColor("#E9F0F5")),
        ("BOX", (0, 0), (-1, -1), 0.6, colors.HexColor("#AAB8C2")),
        ("INNERGRID", (0, 0), (-1, -1), 0.3, colors.HexColor("#D1DBE2")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 8),
        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    return table


def text_response_box(st: dict[str, ParagraphStyle], lines: int = 3) -> Table:
    table = Table([[""] for _ in range(lines)], colWidths=[7.3 * inch], rowHeights=[0.3 * inch] * lines)
    table.setStyle(TableStyle([
        ("LINEBELOW", (0, 0), (-1, -1), 0.45, colors.HexColor("#AAB8C2")),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
    ]))
    return table


def choices_table(choices: list[str], st: dict[str, ParagraphStyle]) -> Table:
    rows = [[p(f"[ ]  {choice}", st["body"])] for choice in choices]
    table = Table(rows, colWidths=[7.3 * inch], hAlign="LEFT")
    table.setStyle(TableStyle([
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 1),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 1),
    ]))
    return table


def matrix_rows(question: Question, st: dict[str, ParagraphStyle]) -> list[object]:
    rows: list[object] = []
    if question.prompt:
        rows.append(p(question.prompt, st["question"]))
    rows.append(p("Mark one response for each statement.", st["small"]))
    for statement in question.choices:
        rows.append(KeepTogether([
            p(statement, st["body"]),
            choices_table(question.answers, st),
            Spacer(1, 4),
        ]))
    return rows


def footer(canvas, document) -> None:  # noqa: ANN001
    canvas.saveState()
    width, _ = letter
    canvas.setStrokeColor(colors.HexColor("#D1DBE2"))
    canvas.setLineWidth(0.4)
    canvas.line(0.65 * inch, 0.48 * inch, width - 0.65 * inch, 0.48 * inch)
    canvas.setFont("Helvetica", 7.5)
    canvas.setFillColor(colors.HexColor("#566573"))
    canvas.drawString(0.65 * inch, 0.3 * inch, "Gaze Contingency Project - Qualtrics draft print copy")
    canvas.drawRightString(width - 0.65 * inch, 0.3 * inch, f"Page {document.page}")
    canvas.restoreState()


def render(source: Path, output: Path, title: str, survey_id: str, administration: str) -> None:
    st = styles()
    blocks = parse_advanced_txt(source)
    doc = SimpleDocTemplate(
        str(output), pagesize=letter, leftMargin=0.65 * inch, rightMargin=0.65 * inch,
        topMargin=0.62 * inch, bottomMargin=0.68 * inch, title=title,
        author="Gaze Contingency Project",
    )
    story: list[object] = [
        Spacer(1, 0.25 * inch),
        p(title, st["title"]),
        p("Printable review copy - DRAFT", st["subtitle"]),
        meta_table([
            ("Qualtrics survey", survey_id),
            ("Administration", administration),
            ("Status", "Draft; freeze wording, order, scoring, requiredness and IRB alignment before collection."),
        ], st),
        Spacer(1, 0.2 * inch),
        Table([[p("This static PDF is for protocol review and experimenter printing. Responses should be collected in the Qualtrics draft so export tags and timestamps remain available.", st["callout"])]], colWidths=[7.3 * inch], style=TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#FFF6DD")),
            ("BOX", (0, 0), (-1, -1), 0.5, colors.HexColor("#E1C36A")),
            ("LEFTPADDING", (0, 0), (-1, -1), 9),
            ("RIGHTPADDING", (0, 0), (-1, -1), 9),
            ("TOPPADDING", (0, 0), (-1, -1), 8),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ])),
        PageBreak(),
    ]

    first_block = True
    for block in blocks:
        if not first_block:
            story.append(PageBreak())
        first_block = False
        story.append(p(block.name, st["block"]))
        for item in block.items:
            if item == "pagebreak":
                story.append(PageBreak())
                continue
            assert isinstance(item, Question)
            label = item.question_id or "Question"
            kind = item.kind.split(":", 1)[0]
            if kind == "DB":
                story.append(KeepTogether([
                    p(f"{label} - Instructions", st["question"]),
                    Table([[p(item.prompt, st["callout"])]], colWidths=[7.3 * inch], style=TableStyle([
                        ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F3F7FA")),
                        ("BOX", (0, 0), (-1, -1), 0.4, colors.HexColor("#C3D0D9")),
                        ("LEFTPADDING", (0, 0), (-1, -1), 8),
                        ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                        ("TOPPADDING", (0, 0), (-1, -1), 7),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
                    ])),
                ]))
            elif kind == "TE":
                story.append(p(f"{label}", st["question"]))
                story.append(p(item.prompt, st["body"]))
                story.append(text_response_box(st, lines=5 if "Essay" in item.kind else 2))
                story.append(Spacer(1, 6))
            elif kind == "MC":
                story.append(p(f"{label}", st["question"]))
                story.append(p(item.prompt, st["body"]))
                story.append(choices_table(item.choices, st))
                story.append(Spacer(1, 6))
            elif kind == "Matrix":
                story.append(p(f"{label}", st["question"]))
                story.extend(matrix_rows(item, st))
                story.append(Spacer(1, 6))

    output.parent.mkdir(parents=True, exist_ok=True)
    doc.build(story, onFirstPage=footer, onLaterPages=footer)


def main() -> None:
    jobs = [
        (
            "01-baseline-pre-task.txt",
            "gaze-study-baseline-pre-task.pdf",
            "Gaze Study - Baseline and Pre-Task Survey",
            "SV_5u2FpCk1228bfPE",
            "Once before headset use and the search task",
        ),
        (
            "02-post-voice-block.txt",
            "gaze-study-post-voice-block.pdf",
            "Gaze Study - Post-Voice-Block Measures",
            "SV_cwsLpEVffEIo9SK",
            "After each generic or self-similar voice block",
        ),
        (
            "03-post-session.txt",
            "gaze-study-post-session-safety.pdf",
            "Gaze Study - Post-Session and Safety Survey",
            "SV_emqdHkeDg3gjYpw",
            "After removing the headset at the end of the session",
        ),
    ]
    for source_name, output_name, title, survey_id, administration in jobs:
        render(SOURCE_DIR / source_name, OUTPUT_DIR / output_name, title, survey_id, administration)


if __name__ == "__main__":
    main()

