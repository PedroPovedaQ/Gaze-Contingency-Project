"""Build Qualtrics Advanced TXT drafts from the study questionnaire register.

The generated files are intentionally drafts. They preserve source item IDs and
make the proposed administration boundary visible so the IRB-ready item map can
be frozen before collection.
"""

from pathlib import Path


OUT = Path(__file__).resolve().parents[2] / "resources" / "surveys" / "qualtrics"


def block(name: str) -> list[str]:
    return [f"[[Block:{name}]]"]


def text(qid: str, prompt: str) -> list[str]:
    return ["[[Question:DB]]", f"[[ID:{qid}]]", prompt]


def mc(qid: str, prompt: str, choices: list[str], multiple: bool = False) -> list[str]:
    subtype = "MultipleAnswer" if multiple else "SingleAnswer"
    lines = [f"[[Question:MC:{subtype}:Vertical]]", f"[[ID:{qid}]]", prompt, "[[Choices]]"]
    return lines + choices


def te(qid: str, prompt: str, essay: bool = False) -> list[str]:
    subtype = "Essay" if essay else "SingleLine"
    return [f"[[Question:TE:{subtype}]]", f"[[ID:{qid}]]", prompt]


def matrix(qid: str, prompt: str, statements: list[str], answers: list[str]) -> list[str]:
    lines = ["[[Question:Matrix]]", f"[[ID:{qid}]]", prompt, "[[Choices]]"]
    lines += statements + ["[[AdvancedAnswers]]"]
    for i, answer in enumerate(answers, 1):
        lines += [f"[[Answer:{i}]]", answer]
    return lines


LIKERT_1_7 = [
    "1 — Strongly disagree",
    "2",
    "3",
    "4",
    "5",
    "6",
    "7 — Strongly agree",
]
SSQ_0_3 = ["0 — None", "1 — Slight", "2 — Moderate", "3 — Severe"]
TLX_0_10 = ["0 — Very low / perfect", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10 — Very high / failure"]


SSQ_ITEMS = [
    "General discomfort",
    "Fatigue",
    "Headache",
    "Eyestrain",
    "Difficulty focusing",
    "Increased salivation",
    "Sweating",
    "Nausea",
    "Difficulty concentrating",
    "Fullness of head",
    "Blurred vision",
    "Dizziness with eyes open",
    "Dizziness with eyes closed",
    "Vertigo",
    "Stomach awareness",
    "Burping",
]

GUO_SECTIONS = [
    ("Co-presence — Biocca et al. 2001", [
        "I noticed the virtual character.",
        "The virtual character noticed me.",
        "The virtual character’s presence was obvious to me.",
        "My presence was obvious to the virtual character.",
        "The virtual character caught my attention.",
        "I caught the virtual character’s attention.",
    ]),
    ("Attentional allocation — Biocca et al. 2001", [
        "I was easily distracted from the virtual character when other things were going on.",
        "The virtual character was easily distracted from me when other things were going on.",
        "I remained focused on the virtual character throughout our interaction.",
        "The virtual character remained focused on me throughout our interaction.",
        "The virtual character did not receive my full attention.",
        "I did not receive the virtual character’s full attention.",
    ]),
    ("Perceived intelligence — Moussawi and Koufaris 2019", [
        "The virtual character was able to operate without my intervention.",
        "The virtual character was aware of the virtual environment.",
        "The virtual character was able to set and pursue tasks by themselves in anticipation of future needs.",
        "The virtual character was able to complete tasks quickly.",
        "The virtual character was able to find and process the necessary information for completing the task.",
        "The virtual character was able to adapt/adjust their behavior based on prior events.",
    ]),
    ("Intelligence comparison — Guo et al. 2024", [
        "Do you think the virtual character was more intelligent than you?",
    ]),
    ("Eerie — Zibrek et al. 2018", [
        "I found the virtual character appealing.",
        "I found the virtual character eerie.",
        "I found the virtual character familiar.",
    ]),
    ("Likability — Reysen 2005", [
        "This virtual character is friendly.",
        "This virtual character is likable.",
        "This virtual character is warm.",
        "This virtual character is approachable.",
        "I would ask this virtual character for advice.",
        "I would like this virtual character as a coworker.",
        "I would like this virtual character as a roommate.",
        "I would like to be friends with this virtual character.",
        "This virtual character is physically attractive.",
        "This virtual character is similar to me.",
        "This virtual character is knowledgeable.",
    ]),
    ("Believability — Lam et al. 2023", [
        "Rate the believability of the virtual character’s voice and appearance combined.",
        "Rate how well you agree with the following: I would expect the virtual character to sound like this from the way they look.",
        "Rate how well you agree with the following: I would expect the virtual character to look like this from the way they sound.",
    ]),
    ("Perceived anthropomorphism — Moussawi and Koufaris 2019", [
        "The virtual character is able to speak like a human.",
        "The virtual character can be happy.",
        "The virtual character can be friendly.",
        "The virtual character can be respectful.",
        "The virtual character can be funny.",
        "The virtual character can be caring.",
    ]),
]


IMI_SECTIONS = [
    ("Interest/enjoyment", [
        "I enjoyed doing this activity very much.",
        "This activity was fun to do.",
        "I thought this was a boring activity. [R]",
        "This activity did not hold my attention at all. [R]",
        "I would describe this activity as very interesting.",
        "I thought this activity was quite enjoyable.",
        "While I was doing this activity, I was thinking about how much I enjoyed it.",
    ]),
    ("Effort/importance", [
        "I put a lot of effort into this.",
        "I didn’t try very hard to do well at this activity. [R]",
        "I tried very hard on this activity.",
        "It was important to me to do well at this task.",
        "I didn’t put much energy into this. [R]",
    ]),
    ("Pressure/tension", [
        "I did not feel nervous at all while doing this. [R]",
        "I felt very tense while doing this activity.",
        "I was very relaxed in doing this. [R]",
        "I was anxious while working on this task.",
        "I felt pressured while doing these.",
    ]),
    ("Value/usefulness", [
        "I believe this activity could be of some value to me.",
        "I think that doing this activity is useful for this search activity.",
        "I think this is important to do because it can improve my search performance.",
        "I would be willing to do this again because it has some value to me.",
        "I think doing this activity could help me to search for targets.",
        "I believe doing this activity could be beneficial to me.",
        "I think this is an important activity.",
    ]),
]


def baseline() -> list[str]:
    out = ["[[AdvancedFormat]]", *block("Study information and baseline"), *text("BASE_INTRO", "DRAFT — Follow My Voice: Gaze-Contingent XR Search with a Self-Similar Agent. This survey is for study setup and baseline measures. Use the study code only; do not enter your name. The instrument map remains subject to IRB and pilot review."), *te("BASE_STUDY_CODE", "Study code (assigned by the researcher):")]
    out += mc("BASE_AGE", "Age range:", ["18–24", "25–34", "35–44", "45–54", "55–64", "65+", "Prefer not to answer"])
    out += mc("BASE_GENDER", "Gender identity (select all that apply):", ["Woman", "Man", "Nonbinary", "Another identity", "Prefer not to answer"], multiple=True)
    out += te("BASE_GENDER_OTHER", "If you selected Another identity, please describe (optional):")
    out += mc("BASE_CORRECTION", "Do you use glasses or contact lenses?", ["Neither", "Glasses", "Contact lenses", "Both", "Prefer not to answer"])
    out += mc("BASE_MR", "How often have you used virtual- or mixed-reality headsets?", ["Never", "Once or twice", "Several times", "Monthly", "Weekly or more"])
    out += mc("BASE_EYE", "Have you used eye-tracking technology before?", ["No", "Yes", "Unsure"])
    out += mc("BASE_VOICE", "Have you used a synthetic or cloned version of your own voice before?", ["No", "Yes", "Unsure"])
    out += matrix("BASE_AI_FAMILIARITY", "How familiar are you with AI voice assistants?", ["Familiarity"], ["1 — Not at all familiar", "2", "3", "4", "5", "6", "7 — Extremely familiar"])
    out += ["[[PageBreak]]", *block("Baseline simulator sickness"), *text("BASE_SSQ_INTRO", "Rate each symptom now, before headset use. Use 0 = none, 1 = slight, 2 = moderate, 3 = severe."), *matrix("BASE_SSQ", "Baseline Simulator Sickness Questionnaire", SSQ_ITEMS, SSQ_0_3)]
    return out


def post_block() -> list[str]:
    out = ["[[AdvancedFormat]]", *block("Voice block identifiers"), *text("BLOCK_INTRO", "DRAFT — Complete this form immediately after the current voice block, before starting the next block. The researcher records the condition fields; do not infer or change them."), *te("BLOCK_STUDY_CODE", "Study code:"), *te("BLOCK_NUMBER", "Block number (researcher entry):"), *te("BLOCK_VOICE_CONDITION", "Voice condition code (researcher entry):"), *te("BLOCK_ORDER", "Condition order code (researcher entry):")]
    out += ["[[PageBreak]]", *block("Workload"), *matrix("BLOCK_TLX", "Raw NASA-TLX. Rate workload during this block. Performance uses 0 = perfect and 10 = failure.", ["Mental Demand — How mentally demanding was this block?", "Physical Demand — How physically demanding was this block?", "Temporal Demand — How hurried or rushed did you feel during this block?", "Performance — How unsuccessful were you in accomplishing the task?", "Effort — How hard did you have to work to achieve your level of performance?", "Frustration — How insecure, discouraged, irritated, stressed, or annoyed did you feel?"], TLX_0_10)]
    out += ["[[PageBreak]]", *block("Intrinsic Motivation Inventory"), *text("IMI_INTRO", "Rate each statement about the search activity you just completed. Use 1 = not at all true and 7 = very true. Items marked [R] are reverse-keyed for scoring; keep the raw response." )]
    for idx, (name, items) in enumerate(IMI_SECTIONS, 1):
        out += matrix(f"IMI_{idx}", f"IMI — {name}", items, ["1 — Not at all true", "2", "3", "4", "5", "6", "7 — Very true"])
    out += ["[[PageBreak]]", *block("Voice and assistance checks"), *matrix("BLOCK_CHECKS", "Rate each statement about this voice block from 1 = strongly disagree to 7 = strongly agree.", ["The hints seemed responsive to what I was looking at.", "The hints were helpful.", "The hints were distracting.", "The speaking voice sounded like my own voice.", "The speaking voice felt familiar.", "I trusted the spoken guidance.", "I felt comfortable hearing this voice.", "The voice felt uncanny or unsettling.", "I relied on the spoken hints when deciding where to search next.", "The warmer/colder feedback helped me narrow down the target location."], LIKERT_1_7)]
    out += mc("BLOCK_HEARD", "Did you hear all prompts clearly?", ["Yes", "No", "Unsure"])
    out += mc("BLOCK_FAULT", "Did any prompt repeat, cut off, arrive late, or use the wrong voice?", ["No", "Yes", "Unsure"])
    out += te("BLOCK_FAULT_NOTE", "If yes, describe what happened (optional):", essay=True)
    out += mc("BLOCK_BREAK", "Did you take an unplanned break or experience an interruption?", ["No", "Yes"])
    out += te("BLOCK_BREAK_NOTE", "If yes, describe the interruption (optional):", essay=True)
    out += ["[[PageBreak]]", *block("Agent perception source bank — Guo et al. 2024"), *text("GUO_INTRO", "Source-bank draft from Guo et al. (2024), Table A1. Rate the virtual voice agent you just experienced. All items use 1 = very strongly disagree and 7 = very strongly agree. Appearance-specific items are retained for review and may be marked not applicable in the frozen participant form." )]
    for idx, (section, items) in enumerate(GUO_SECTIONS, 1):
        out += matrix(f"GUO_{idx}", section, items, ["1 — Very strongly disagree", "2", "3", "4", "5", "6", "7 — Very strongly agree"])
    out += te("GUO_Q43", "Please provide additional comments on your overall experience:", essay=True)
    return out


def post_session() -> list[str]:
    out = ["[[AdvancedFormat]]", *block("Post-exposure safety"), *text("POST_INTRO", "DRAFT — Complete after removing the headset. Tell the researcher immediately if symptoms are severe or worsening."), *matrix("POST_SSQ", "Post-exposure Simulator Sickness Questionnaire. Use 0 = none, 1 = slight, 2 = moderate, 3 = severe.", SSQ_ITEMS, SSQ_0_3), *mc("POST_STOP", "Did you stop or pause because of symptoms?", ["No", "Yes"]), *mc("POST_READY", "Do you feel ready to leave the lab?", ["Yes", "No — researcher follow-up required"])]
    out += ["[[PageBreak]]", *block("Voice comparison and interview"), *mc("POST_VOICE_PREF", "Which voice did you prefer?", ["Generic voice", "Voice resembling me", "No preference", "Unsure"]), *te("POST_VOICE_REASON", "Why did you choose that response?", essay=True), *te("POST_HELPFUL", "Which voice or combination felt most helpful, and why?", essay=True), *te("POST_COMFORT", "Which voice or combination felt most comfortable, and why?", essay=True), *te("POST_SIMILARITY", "Did you believe the self-similar voice resembled your voice? Why or why not?", essay=True), *te("POST_DIALOGUE", "Did hearing the self-similar voice change how you experienced your own thoughts or internal dialogue? Please describe.", essay=True), *te("POST_CONCERNS", "Did hearing the self-similar voice create any concern about misuse or how the voice was created?", essay=True), *te("POST_CHANGES", "What would you change about the hints, voice, or task?", essay=True), *te("POST_INTERVIEW", "Researcher notes from the semi-structured final interview (search strategy, useful or distracting feedback, voice experience, comfort, and suggested changes):", essay=True)]
    return out


def write(name: str, lines: list[str]) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / name).write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    write("01-baseline-pre-task.txt", baseline())
    write("02-post-voice-block.txt", post_block())
    write("03-post-session.txt", post_session())
