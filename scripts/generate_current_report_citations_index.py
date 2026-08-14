#!/usr/bin/env python3
"""Generate the current IRB/procedure citation index and restricted-paper note."""

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "pdf" / "Current Report Citations"


def styles():
    sheet = getSampleStyleSheet()
    sheet["Title"].fontName = "Helvetica-Bold"
    sheet["Title"].fontSize = 21
    sheet["Title"].leading = 25
    sheet["Title"].textColor = colors.HexColor("#18364F")
    sheet["Heading2"].fontName = "Helvetica-Bold"
    sheet["Heading2"].fontSize = 12.5
    sheet["Heading2"].textColor = colors.HexColor("#18364F")
    sheet.add(ParagraphStyle(name="Subtitle", parent=sheet["Normal"], fontSize=10.5,
                             leading=14, alignment=TA_CENTER, textColor=colors.HexColor("#4F6374")))
    sheet.add(ParagraphStyle(name="Body", parent=sheet["BodyText"], fontSize=9.6, leading=14, spaceAfter=8))
    sheet.add(ParagraphStyle(name="Cell", parent=sheet["BodyText"], fontSize=8, leading=10))
    sheet.add(ParagraphStyle(name="Note", parent=sheet["BodyText"], fontSize=8.8, leading=12,
                             textColor=colors.HexColor("#53606B"), backColor=colors.HexColor("#F1F4F6"),
                             borderPadding=8, spaceBefore=6, spaceAfter=8))
    return sheet


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.HexColor("#66737D"))
    canvas.drawString(0.65 * inch, 0.42 * inch, "Gaze Contingency current-report citations")
    canvas.drawRightString(7.85 * inch, 0.42 * inch, f"Page {doc.page}")
    canvas.restoreState()


def make_kaschub_note(sheet):
    path = OUT / "2026 - Kaschub - VR Intelligent Assistant Referential Interaction - Citation and Summary.pdf"
    doc = SimpleDocTemplate(str(path), pagesize=letter, rightMargin=0.8 * inch, leftMargin=0.8 * inch,
                            topMargin=0.72 * inch, bottomMargin=0.68 * inch,
                            title="Kaschub et al. 2026 - Citation and Summary")
    story = [
        Paragraph("PUBLISHER-RESTRICTED PAPER", ParagraphStyle(
            name="Kicker", parent=sheet["Normal"], fontName="Helvetica-Bold", fontSize=9,
            alignment=TA_CENTER, textColor=colors.HexColor("#A04B2A"))),
        Spacer(1, 0.14 * inch),
        Paragraph("Comparing Referential Interactions with an Intelligent Assistant in Virtual Reality", sheet["Title"]),
        Paragraph("Lina Kaschub, Bado Volckers, Ugur Turhan, Philipp Huesmann, Lucie Kruse, and Frank Steinicke", sheet["Subtitle"]),
        Paragraph("IEEE VR 2026, pp. 453-463", sheet["Subtitle"]),
        Paragraph('<link href="https://doi.org/10.1109/VR67842.2026.00066">doi:10.1109/VR67842.2026.00066</link>', sheet["Subtitle"]),
        Spacer(1, 0.28 * inch),
        Paragraph("Why the report cites it", sheet["Heading2"]),
        Paragraph(
            "The study compares speech descriptions, gaze, and pointing as ways to reference objects while working "
            "with an intelligent assistant in VR. Its 39-participant evaluation reports task time, prompt counts, "
            "NASA-TLX, and UEQ outcomes. Gaze performed competitively while requiring little explicit user action, "
            "which supports treating gaze as contextual input for assistance rather than only as a selection method.",
            sheet["Body"],
        ),
        Paragraph("Limits on transfer", sheet["Heading2"]),
        Paragraph(
            "This paper supports gaze as an efficient referential signal. It does not validate the Gaze Contingency "
            "project's dwell threshold, hint timing, object layout, or gaze-classification rules. Those parameters still "
            "require headset-specific pilot testing.",
            sheet["Body"],
        ),
        Paragraph(
            "The full IEEE PDF was not available through a lawful open-access source during collection. This file is an "
            "original citation and summary, not the paper. Retrieve the full text through UCF Libraries or the authors.",
            sheet["Note"],
        ),
    ]
    doc.build(story, onFirstPage=footer)


def make_index(sheet):
    path = OUT / "00 - INDEX - Current Report Citations.pdf"
    doc = SimpleDocTemplate(str(path), pagesize=letter, rightMargin=0.6 * inch, leftMargin=0.6 * inch,
                            topMargin=0.62 * inch, bottomMargin=0.68 * inch,
                            title="Current Report Citations - Gaze Contingency")
    records = [
        ("1988", "Hart & Staveland", "Development of NASA-TLX", "Full PDF", "Subjective workload measure"),
        ("2023", "Zhang et al.", "See or Hear? Audio and visual guidance for AR search", "Full PDF", "Search procedure, guidance modality, NASA-TLX"),
        ("2024", "Chiossi et al.", "Searching Across Realities", "Full PDF", "Visual-search procedure, eye tracking, ERP, NASA-TLX"),
        ("2024", "Guo et al.", "Self-similar appearance and voice", "Full PDF", "2 x 2 voice design, voice capture, post-condition surveys"),
        ("2026", "Kaschub et al.", "Referential interaction with a VR intelligent assistant", "Citation/summary", "Gaze as low-effort assistant context; workload and UX"),
    ]
    story = [
        Paragraph("Current Report Citations", sheet["Title"]),
        Paragraph("Sources currently cited by the IRB draft and experiment procedure", sheet["Subtitle"]),
        Spacer(1, 0.18 * inch),
        Paragraph(
            "Source documents checked: <b>docs/irb/project/irb-documentation-draft.tex</b> and "
            "<b>docs/experiment-procedure.md</b>. The older project proposal was not used because its external "
            "references.bib file is missing and several descriptions are superseded by the current procedure.",
            sheet["Body"],
        ),
        Paragraph(
            "This folder contains four full papers and one clearly labeled citation/summary for a restricted IEEE VR "
            "paper. Filenames use year, first author, and a recognizable short title.", sheet["Note"]),
        Spacer(1, 0.12 * inch),
    ]
    rows = [["Year", "Citation", "Local status", "Role in current report"]]
    for year, author, title, status, role in records:
        rows.append([
            year,
            Paragraph(f"<b>{author}</b><br/>{title}", sheet["Cell"]),
            Paragraph(status, sheet["Cell"]),
            Paragraph(role, sheet["Cell"]),
        ])
    table = Table(rows, colWidths=[0.48 * inch, 2.72 * inch, 1.05 * inch, 3.05 * inch], repeatRows=1)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#18364F")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#BAC4CC")),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F3F6F8")]),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    story.extend([
        table,
        Spacer(1, 0.24 * inch),
        Paragraph("Important distinction", sheet["Heading2"]),
        Paragraph(
            "NASA-TLX is the cited subjective workload instrument. Chiossi and Zhang support its placement in related "
            "XR search procedures, while Guo supports the self-similar voice manipulation. Kaschub supports gaze as an "
            "assistant reference channel. None independently validates this project's exact timing or gaze thresholds.",
            sheet["Body"],
        ),
    ])
    doc.build(story, onFirstPage=footer)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    sheet = styles()
    make_kaschub_note(sheet)
    make_index(sheet)


if __name__ == "__main__":
    main()
