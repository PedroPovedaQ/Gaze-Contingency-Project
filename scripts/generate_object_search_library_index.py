#!/usr/bin/env python3
"""Generate the object-search paper library index and the VSVR meeting abstract."""

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "pdf" / "Object Search Papers 2018-2026"

INCLUDED = [
    (2018, "Olk", "Measuring Visual Search and Distraction in Immersive VR", "Virtual Reality"),
    (2019, "Trepkowski", "Narrow FOV and Information Density in AR Visual Search", "IEEE VR"),
    (2020, "Marquardt", "Nonvisual and Visual Guidance for Narrow FOV AR", "IEEE VR"),
    (2020, "Van Dam", "Drone AR Bridge Inspection Visual Search", "ISARC"),
    (2021, "Binetti", "Visual and Auditory Cues for Out of View AR Objects", "IEEE VR"),
    (2021, "David", "Peripheral Vision in 3D Real World Scene Search", "Journal of Vision"),
    (2022, "Hadnett-Hunter", "VSVR Meeting Abstract - Citation and Summary", "Journal of Vision; meeting abstract summary"),
    (2022, "Kim", "Search Among Physical and Virtual Objects", "IEEE ISMAR"),
    (2023, "Kumaran", "Navigation Aids Search Performance and Object Recall", "IEEE ISMAR"),
    (2023, "Warden", "Imperfect Automated Cues in AR Visual Search", "HFES Annual Meeting"),
    (2023, "Zhang", "See or Hear Visual and Audio Hints for AR Search", "IEEE ISMAR"),
    (2024, "Chiossi", "Reality Virtuality Continuum Visual Search and Eye Tracking", "PACM HCI"),
    (2024, "Chiossi", "Searching Across Realities Eye Tracking and ERP", "IEEE ISMAR"),
    (2024, "Slavuljica", "NeighboAR Proximity and Gaze Object Retrieval", "ACM CHI"),
    (2024, "Stein", "Eye and Head Movements in Extended Field Visual Search", "Scientific Reports"),
    (2025, "Hidayat", "Remote Multiplayer AR Hide and Seek with Hot Cold Hints", "Electronics"),
    (2025, "Kelley", "Cueing in 360 Degree Multiple Target Visual Search", "ACM VRST"),
    (2025, "Kim", "On the Go with AR and Augmentation Density", "IEEE TVCG / IEEE VR"),
    (2025, "Yu", "Cost of Virtuality Switching in AR Search", "IEEE ISMAR"),
    (2026, "Radulescu", "Resource Rational Visual Search in Virtual Reality", "Open Mind"),
]

UNAVAILABLE = [
    (2018, "Bork", "Towards Efficient Visual Guidance in Limited Field-of-View Head-Mounted Displays", "IEEE ISMAR / TVCG", "10.1109/TVCG.2018.2868584"),
    (2020, "Lee", "Effects of Background Complexity and Viewing Distance on an AR Visual Search Task", "IEEE ISMAR Adjunct", "10.1109/ISMAR-Adjunct51615.2020.00057"),
    (2020, "Wallgrun", "A Comparison of Visual Attention Guiding Approaches for 360-Degree Image-Based VR Tours", "IEEE VR", "10.1109/VR46266.2020.00026"),
    (2022, "Warden", "Visual Search in Augmented Reality: Effect of Target Cue Type and Location", "HFES Annual Meeting", "10.1177/1071181322661260"),
    (2025, "Choi", "Distance-Adaptive Visual Guidance for Spatial Awareness Formation in Out-of-View AR", "IEEE ISMAR Adjunct", "publisher listing; DOI not yet indexed"),
]


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(colors.HexColor("#666666"))
    canvas.drawString(0.65 * inch, 0.42 * inch, "Gaze Contingency object-search literature library")
    canvas.drawRightString(7.85 * inch, 0.42 * inch, f"Page {doc.page}")
    canvas.restoreState()


def make_abstract(styles):
    path = OUT / "2022 - Hadnett-Hunter - VSVR Meeting Abstract - Citation and Summary.pdf"
    doc = SimpleDocTemplate(str(path), pagesize=letter, rightMargin=0.8 * inch, leftMargin=0.8 * inch,
                            topMargin=0.7 * inch, bottomMargin=0.65 * inch,
                            title="Visual Search in Virtual Reality (VSVR): A Visual Search Toolbox for Virtual Reality")
    abstract = (
        "The authors introduce VSVR, a Unity-based toolbox for building controlled visual-search experiments in virtual "
        "reality while retaining more natural viewing and interaction than conventional 2D studies. They demonstrate it "
        "with three paradigms: feature search, wide-field search with eccentricity effects, and depth-plane search. The "
        "toolbox is positioned as a way to improve ecological validity without giving up experimental control."
    )
    story = [
        Paragraph("MEETING ABSTRACT", styles["Kicker"]),
        Spacer(1, 0.13 * inch),
        Paragraph("Visual Search in Virtual Reality (VSVR): A Visual Search Toolbox for Virtual Reality", styles["Title"]),
        Spacer(1, 0.18 * inch),
        Paragraph("Jacob Hadnett-Hunter, Eamonn O'Neill, and Michael J. Proulx", styles["Authors"]),
        Paragraph("Journal of Vision 22(3), Article 19 (2022)", styles["Meta"]),
        Paragraph('<link href="https://doi.org/10.1167/jov.22.3.19">doi:10.1167/jov.22.3.19</link>', styles["Meta"]),
        Spacer(1, 0.3 * inch),
        Paragraph("Summary", styles["Heading2"]),
        Paragraph(abstract, styles["Body"]),
        Spacer(1, 0.3 * inch),
        Paragraph("Document note", styles["Heading2"]),
        Paragraph(
            "This publication is a one-page, peer-reviewed meeting abstract, not a full-length article. The publisher "
            "PDF was not downloadable during collection, so this local file contains a citation and an original summary, "
            "not a reproduction of the source. The authoritative record is linked by DOI above.", styles["Note"]),
    ]
    doc.build(story)


def make_index(styles):
    path = OUT / "00 - INDEX - Object Search Papers 2018-2026.pdf"
    doc = SimpleDocTemplate(str(path), pagesize=letter, rightMargin=0.55 * inch, leftMargin=0.55 * inch,
                            topMargin=0.58 * inch, bottomMargin=0.62 * inch,
                            title="Object Search Papers 2018-2026 - Local PDF Inventory")
    story = [
        Paragraph("Object Search Papers, 2018-2026", styles["Title"]),
        Paragraph("Local PDF inventory for the Gaze Contingency study", styles["Authors"]),
        Spacer(1, 0.18 * inch),
        Paragraph(
            "This folder contains 19 full research papers plus one citation-and-summary PDF, with sortable filenames in the form "
            "<b>Year - First Author - Obvious Short Title.pdf</b>. It covers immersive VR/AR object search, gaze and "
            "head behavior, proximity or directional hints, cross-reality search, and visual-attention guidance.",
            styles["Body"],
        ),
        Paragraph(
            "The 2022 VSVR entry is explicitly a citation and summary for a one-page meeting abstract. The 2026 "
            "Radulescu entry is a full-text, "
            "reflowed PDF generated from the article's CC BY 4.0 PubMed Central JATS record; publisher pagination differs.",
            styles["Note"],
        ),
        Spacer(1, 0.14 * inch),
        Paragraph("Included locally", styles["Heading2"]),
    ]
    rows = [["Year", "Paper / local filename", "Venue"]]
    for year, author, title, venue in INCLUDED:
        rows.append([str(year), Paragraph(f"<b>{author}</b> - {title}", styles["Cell"]), Paragraph(venue, styles["Cell"])] )
    table = Table(rows, colWidths=[0.48 * inch, 5.15 * inch, 1.65 * inch], repeatRows=1)
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#17324D")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 7.6),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#B9C3CC")),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F3F6F8")]),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    story.extend([table, PageBreak(), Paragraph("Publisher-restricted copies not included", styles["Heading2"])])
    story.append(Paragraph(
        "The following five records were part of the requested inventory, but no lawful open-access PDF was located. "
        "Their titles and identifiers are preserved here so they can be retrieved through UCF Libraries or an author request.",
        styles["Body"],
    ))
    missing_rows = [["Year", "Paper", "Venue / identifier"]]
    for year, author, title, venue, identifier in UNAVAILABLE:
        missing_rows.append([
            str(year),
            Paragraph(f"<b>{author}</b> - {title}", styles["Cell"]),
            Paragraph(f"{venue}<br/><font color='#42566A'>{identifier}</font>", styles["Cell"]),
        ])
    missing = Table(missing_rows, colWidths=[0.48 * inch, 4.65 * inch, 2.15 * inch], repeatRows=1)
    missing.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#713B2E")),
        ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
        ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
        ("FONTSIZE", (0, 0), (-1, -1), 8),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#C9B8B2")),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#FAF4F2")]),
        ("LEFTPADDING", (0, 0), (-1, -1), 5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 5),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    story.extend([
        missing,
        Spacer(1, 0.22 * inch),
        Paragraph("Use in the experiment design", styles["Heading2"]),
        Paragraph(
            "Start with Kim (2022), Chiossi (2024), Yu (2025), and Radulescu (2026) for task structure and gaze measures. "
            "Use Slavuljica (2024), Hidayat (2025), Zhang (2023), Marquardt (2020), Binetti (2021), and Kelley (2025) "
            "for proximity, directional, modality, and cueing choices.",
            styles["Body"],
        ),
    ])
    doc.build(story, onFirstPage=footer, onLaterPages=footer)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="Kicker", parent=styles["Normal"], fontName="Helvetica-Bold",
                              fontSize=9, textColor=colors.HexColor("#A04B2A"), alignment=TA_CENTER, leading=11))
    styles.add(ParagraphStyle(name="Authors", parent=styles["Normal"], fontSize=10.5,
                              textColor=colors.HexColor("#42566A"), alignment=TA_CENTER, leading=14))
    styles.add(ParagraphStyle(name="Meta", parent=styles["Normal"], fontSize=9,
                              textColor=colors.HexColor("#536779"), alignment=TA_CENTER, leading=12))
    styles.add(ParagraphStyle(name="Body", parent=styles["BodyText"], fontSize=9.6, leading=14, spaceAfter=8))
    styles.add(ParagraphStyle(name="Note", parent=styles["BodyText"], fontSize=8.7, leading=12,
                              textColor=colors.HexColor("#53606B"), backColor=colors.HexColor("#F1F4F6"),
                              borderPadding=8, spaceBefore=6, spaceAfter=8))
    styles.add(ParagraphStyle(name="Cell", parent=styles["BodyText"], fontSize=7.6, leading=9.4))
    styles["Title"].fontName = "Helvetica-Bold"
    styles["Title"].fontSize = 21
    styles["Title"].leading = 25
    styles["Title"].textColor = colors.HexColor("#17324D")
    styles["Heading2"].fontName = "Helvetica-Bold"
    styles["Heading2"].fontSize = 12.5
    styles["Heading2"].textColor = colors.HexColor("#17324D")
    make_abstract(styles)
    make_index(styles)


if __name__ == "__main__":
    main()
