"""Render annotated review PDFs with provenance notes for every survey group."""

from __future__ import annotations

from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import PageBreak, Paragraph, Spacer, Table, TableStyle

from render_surveys_pdf import (
    OUTPUT_DIR,
    ROOT,
    SOURCE_DIR,
    Question,
    ascii_text,
    choices_table,
    footer,
    matrix_rows,
    meta_table,
    p,
    parse_advanced_txt,
    styles,
    text_response_box,
)
from reportlab.platypus import SimpleDocTemplate


SOURCE_NOTES = {
    "BASE_INTRO": "Study-created protocol and participant instruction; no external instrument.",
    "BASE_STUDY_CODE": "Study-created administrative field; no external instrument.",
    "BASE_AGE": "Study-created demographic item; no external instrument.",
    "BASE_GENDER": "Study-created demographic item; no external instrument.",
    "BASE_GENDER_OTHER": "Study-created demographic follow-up; no external instrument.",
    "BASE_CORRECTION": "Study-created background item; no external instrument.",
    "BASE_MR": "Study-created prior-experience item; no external instrument.",
    "BASE_EYE": "Study-created prior-experience item; no external instrument.",
    "BASE_VOICE": "Study-created prior-experience item; no external instrument.",
    "BASE_AI_FAMILIARITY": "Study-created familiarity rating; no validated scale is claimed.",
    "BASE_SSQ_INTRO": "Kennedy et al. (1993), Simulator Sickness Questionnaire (SSQ).",
    "BASE_SSQ": "Kennedy et al. (1993), Simulator Sickness Questionnaire (SSQ), 16 symptoms and 0-3 anchors.",
    "BLOCK_INTRO": "Study-created block handoff and researcher-entry instructions; no external instrument.",
    "BLOCK_STUDY_CODE": "Study-created administrative field; no external instrument.",
    "BLOCK_NUMBER": "Study-created administrative field; no external instrument.",
    "BLOCK_VOICE_CONDITION": "Study-created condition label; no external instrument.",
    "BLOCK_ORDER": "Study-created counterbalancing label; no external instrument.",
    "BLOCK_TLX": "Hart and Staveland (1988), NASA Task Load Index. This draft uses the project's unweighted 0-10 response adaptation.",
    "IMI_INTRO": "Center for Self-Determination Theory, official IMI item bank; four-subscale selection follows Kao et al. (2021), Section 4.3.3.",
    "IMI_1": "Intrinsic Motivation Inventory, Interest/enjoyment subscale. Official IMI item bank; selection precedent: Kao et al. (2021), Section 4.3.3.",
    "IMI_2": "Intrinsic Motivation Inventory, Effort/importance subscale. Official IMI item bank; selection precedent: Kao et al. (2021), Section 4.3.3.",
    "IMI_3": "Intrinsic Motivation Inventory, Pressure/tension subscale. Official IMI item bank; selection precedent: Kao et al. (2021), Section 4.3.3.",
    "IMI_4": "Intrinsic Motivation Inventory, Value/usefulness subscale. Official IMI item bank; selection precedent: Kao et al. (2021), Section 4.3.3.",
    "BLOCK_CHECKS": "Study-created voice, guidance and manipulation-check items, informed by the self-similar voice literature (Kao et al., 2021; Guo et al., 2024). Do not treat this group as a validated composite.",
    "BLOCK_HEARD": "Study-created technical quality check; no external instrument.",
    "BLOCK_FAULT": "Study-created technical quality check; no external instrument.",
    "BLOCK_FAULT_NOTE": "Study-created technical incident note; no external instrument.",
    "BLOCK_BREAK": "Study-created session-quality check; no external instrument.",
    "BLOCK_BREAK_NOTE": "Study-created interruption note; no external instrument.",
    "GUO_INTRO": "Guo et al. (2024), Table A1, retained as a source-bank transcription for review.",
    "GUO_1": "Guo et al. (2024), Table A1, Co-presence; original attribution: Biocca, Harms and Gregg (2001).",
    "GUO_2": "Guo et al. (2024), Table A1, Attentional allocation; original attribution: Biocca, Harms and Gregg (2001).",
    "GUO_3": "Guo et al. (2024), Table A1, Perceived intelligence; original attribution: Moussawi and Koufaris (2019).",
    "GUO_4": "Guo et al. (2024), Table A1, Intelligence comparison; study-created item in the Guo paper.",
    "GUO_5": "Guo et al. (2024), Table A1, Eerie; original attribution: Zibrek, Kokkinara and McDonnell (2018).",
    "GUO_6": "Guo et al. (2024), Table A1, Likability; original attribution: Reysen (2005).",
    "GUO_7": "Guo et al. (2024), Table A1, Believability; original attribution: Lam et al. (2023).",
    "GUO_8": "Guo et al. (2024), Table A1, Perceived anthropomorphism; original attribution: Moussawi and Koufaris (2019).",
    "GUO_Q43": "Guo et al. (2024), Table A1, Overall experience open response.",
    "POST_INTRO": "Study-created post-exposure safety instruction; safety procedure is project-specific.",
    "POST_SSQ": "Kennedy et al. (1993), Simulator Sickness Questionnaire (SSQ), 16 symptoms and 0-3 anchors.",
    "POST_STOP": "Study-created safety stop/pause check; not an SSQ scoring item.",
    "POST_READY": "Study-created researcher safety handoff check; not an SSQ scoring item.",
    "POST_VOICE_PREF": "Study-created comparative voice-preference item, informed by Kao et al. (2021) and Guo et al. (2024).",
    "POST_VOICE_REASON": "Study-created qualitative follow-up to the voice-preference item.",
    "POST_HELPFUL": "Study-created qualitative voice/guidance evaluation; informed by Kao et al. (2021) and Guo et al. (2024).",
    "POST_COMFORT": "Study-created qualitative voice evaluation; informed by Kao et al. (2021) and Guo et al. (2024).",
    "POST_SIMILARITY": "Study-created self-similar-voice manipulation check; informed by Kao et al. (2021).",
    "POST_DIALOGUE": "Study-created exploratory qualitative item; no validated mechanism scale is claimed.",
    "POST_CONCERNS": "Study-created voice-safety and misuse concern item; no external instrument.",
    "POST_CHANGES": "Study-created design-feedback item; no external instrument.",
    "POST_INTERVIEW": "Study-created researcher interview notes; no external instrument.",
}


REFERENCES = [
    "Hart, S. G., and Staveland, L. E. (1988). Development of NASA-TLX: Results of empirical and theoretical research. Human Mental Workload. DOI: 10.1016/S0166-4115(08)62386-9.",
    "Kennedy, R. S., Lane, N. E., Berbaum, K. S., and Lilienthal, M. G. (1993). Simulator Sickness Questionnaire. The International Journal of Aviation Psychology, 3(3), 203-220. DOI: 10.1207/s15327108ijap0303_3.",
    "Center for Self-Determination Theory. Intrinsic Motivation Inventory: official instrument and complete item bank. https://selfdeterminationtheory.org/intrinsic-motivation-inventory/.",
    "Kao, D., Ratan, R., Mousas, C., and Magana, A. J. (2021). The Effects of a Self-Similar Avatar Voice in Educational Games. PACMHCI, 5(CHI PLAY). DOI: 10.1145/3474665.",
    "Guo, S., Choi, M., Kao, D., and Mousas, C. (2024). Collaborating with my Doppelganger: The Effects of Self-similar Appearance and Voice of a Virtual Character during a Jigsaw Puzzle Co-solving Task. PACM CGIT, 7(1). DOI: 10.1145/3651288.",
    "Biocca, F., Harms, C., and Gregg, J. (2001). The networked minds measure of social presence: Pilot test of the factor structure and concurrent validity.",
    "Moussawi, S., and Koufaris, M. (2019). Perceived intelligence and perceived anthropomorphism of personal intelligent agents: Scale development and validation.",
    "Zibrek, K., Kokkinara, E., and McDonnell, R. (2018). The effect of realistic appearance of virtual characters in immersive environments. IEEE TVCG, 24(4), 1681-1690. DOI: 10.1109/TVCG.2018.2794638.",
    "Reysen, S. (2005). Construction of a new scale: The Reysen likability scale. Social Behavior and Personality, 33(2), 201-208.",
    "Lam, L., Choi, M., Mukanova, M., Hauser, K., Zhao, F., Mayer, R., Mousas, C., and Adamo-Villani, N. (2023). Effects of body type and voice pitch on perceived audio-visual correspondence and believability of virtual characters.",
]


def source_callout(note: str, st: dict[str, ParagraphStyle]) -> Table:
    source_style = ParagraphStyle(
        "SourceNote", parent=st["small"], fontName="Helvetica-Oblique",
        fontSize=7.7, leading=10, textColor=colors.HexColor("#684B1E"),
    )
    table = Table([[p("Source / provenance: " + ascii_text(note), source_style)]], colWidths=[7.3 * inch])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#FFF6DD")),
        ("BOX", (0, 0), (-1, -1), 0.35, colors.HexColor("#E1C36A")),
        ("LEFTPADDING", (0, 0), (-1, -1), 7),
        ("RIGHTPADDING", (0, 0), (-1, -1), 7),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    return table


def add_question(story: list[object], block_name: str, question: Question, st: dict[str, ParagraphStyle]) -> None:
    qid = question.question_id or "Question"
    kind = question.kind.split(":", 1)[0]
    note = SOURCE_NOTES.get(qid, "Source mapping requires review; no source note was registered for this item.")
    if kind == "DB":
        story.extend([
            p(f"{qid} - Instructions", st["question"]),
            Table([[p(question.prompt, st["callout"])]], colWidths=[7.3 * inch], style=TableStyle([
                ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#F3F7FA")),
                ("BOX", (0, 0), (-1, -1), 0.4, colors.HexColor("#C3D0D9")),
                ("LEFTPADDING", (0, 0), (-1, -1), 8),
                ("RIGHTPADDING", (0, 0), (-1, -1), 8),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
            ])),
            source_callout(note, st), Spacer(1, 5),
        ])
    elif kind == "TE":
        story.extend([p(qid, st["question"]), p(question.prompt, st["body"]), text_response_box(st, lines=5 if "Essay" in question.kind else 2), source_callout(note, st), Spacer(1, 6)])
    elif kind == "MC":
        story.extend([p(qid, st["question"]), p(question.prompt, st["body"]), choices_table(question.choices, st), source_callout(note, st), Spacer(1, 6)])
    elif kind == "Matrix":
        story.append(p(qid, st["question"]))
        story.extend(matrix_rows(question, st))
        story.extend([source_callout(note, st), Spacer(1, 6)])


def render_annotated(source: Path, output: Path, title: str, survey_id: str, administration: str) -> None:
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
        p("Annotated provenance copy - DRAFT", st["subtitle"]),
        meta_table([
            ("Qualtrics survey", survey_id),
            ("Administration", administration),
            ("Annotation key", "Yellow notes identify the source paper, instrument packet or study-created status for each item group."),
        ], st),
        Spacer(1, 0.2 * inch),
        Table([[p("This annotated PDF is for protocol, IRB and committee review. It records provenance and adaptations; it is not the participant-facing form.", st["callout"])]], colWidths=[7.3 * inch], style=TableStyle([
            ("BACKGROUND", (0, 0), (-1, -1), colors.HexColor("#FFF6DD")),
            ("BOX", (0, 0), (-1, -1), 0.5, colors.HexColor("#E1C36A")),
            ("LEFTPADDING", (0, 0), (-1, -1), 9),
            ("RIGHTPADDING", (0, 0), (-1, -1), 9),
            ("TOPPADDING", (0, 0), (-1, -1), 8),
            ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ])),
        PageBreak(),
    ]
    for index, block in enumerate(blocks):
        if index:
            story.append(PageBreak())
        story.append(p(block.name, st["block"]))
        for item in block.items:
            if item == "pagebreak":
                story.append(PageBreak())
            else:
                add_question(story, block.name, item, st)

    story.append(PageBreak())
    story.append(p("References and provenance notes", st["block"]))
    story.append(p("The annotations distinguish published instruments, source-paper adaptations and study-created items. They do not certify that an adaptation is validated for this headset or task.", st["body"]))
    for number, reference in enumerate(REFERENCES, 1):
        story.append(p(f"{number}. {reference}", st["body"]))
        story.append(Spacer(1, 2))
    output.parent.mkdir(parents=True, exist_ok=True)
    doc.build(story, onFirstPage=footer, onLaterPages=footer)


def main() -> None:
    jobs = [
        ("01-baseline-pre-task.txt", "gaze-study-baseline-pre-task-annotated-sources.pdf", "Gaze Study - Baseline and Pre-Task Survey", "SV_5u2FpCk1228bfPE", "Once before headset use and the search task"),
        ("02-post-voice-block.txt", "gaze-study-post-voice-block-annotated-sources.pdf", "Gaze Study - Post-Voice-Block Measures", "SV_cwsLpEVffEIo9SK", "After each generic or self-similar voice block"),
        ("03-post-session.txt", "gaze-study-post-session-safety-annotated-sources.pdf", "Gaze Study - Post-Session and Safety Survey", "SV_emqdHkeDg3gjYpw", "After removing the headset at the end of the session"),
    ]
    for source_name, output_name, title, survey_id, administration in jobs:
        render_annotated(SOURCE_DIR / source_name, OUTPUT_DIR / output_name, title, survey_id, administration)


if __name__ == "__main__":
    main()
