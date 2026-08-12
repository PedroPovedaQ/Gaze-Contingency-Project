# Standardized Voice Recording Script

**Protocol proposal:** This script and upload workflow are proposed. Use only after UCF approves the provider, data path, exact script, and deletion procedure.

Study: **Gaze-Contingent AI Assistance and Self-Similar Voice in Mixed Reality Visual Search**

## Researcher Setup

- Confirm signed consent and voice-cloning authorization before recording.
- Use a private, quiet room, the approved microphone/settings, and the institution-managed encrypted voice workstation.
- Name the file only with study ID, session/run, and recording version; never use a name or email.
- Confirm that the approved provider account has model-improvement sharing disabled and that the private/no-sharing setting is active.
- Confirm that temporary folders do not synchronize to consumer/cloud backup and that the provider credential is not present on the headset.
- Ask the participant to speak naturally at a consistent volume. Do not coach accent, identity, emotion, or impersonation.
- Record only the approved text. Stop if the participant includes a name or other personal information; delete that take and restart.
- Target approximately 1–2 minutes of clear single-speaker audio, consistent with the proposed IVC path. Upload over encrypted TLS, generate only the approved fixed prompt manifest on the staff workstation, and copy prepared clips—not credentials—to the headset.

## Participant Introduction

“I will ask you to read a short neutral passage in your normal speaking voice. Please do not say your name or add personal details. The approved speech service will use this sample to make a private synthetic voice for this study. The synthetic voice can say the study’s fixed phrases even though you did not record those exact phrases. You may stop before or during recording. If you stop, the recording will be deleted and you will not continue in the main experiment, with no penalty.”

Confirm: “Do you want to continue with the recording?”  ☐ Yes ☐ No

## Recording Passage

Today I am taking part in a visual search activity. I will look across several shelves and compare objects by color and shape. Some objects may look similar, so I will take my time and check each area carefully.

The assistant may offer a short hint. A hint can tell me that I am closer, farther away, or looking in a useful direction. I can listen to the hint, keep searching, and make my own choice. If I need a break, I can pause the activity.

Clear speech can change with pace, emphasis, and punctuation. I can say, “Look near the upper shelf,” or ask, “Is the blue shape beside the red one?” I can count: one, two, three, four, five, six, seven, eight, nine, ten.

The quick brown fox moves past the quiet garden. Bright yellow stars appear above purple cubes. Round spheres, narrow cylinders, and small pyramids fill the display. Each sentence gives the system a different pattern of sounds while keeping the content neutral.

I understand that this recording is for the approved research study. I will not say my name, address, phone number, or any other personal information in this recording.

## Quality and Upload Record

Study ID: ____________________  Recording version/take: ____________________ \
Start/end time: ____________________  Duration: ____________________ \
Approved microphone/settings: ____________________ \
Background noise/reverb acceptable? ☐ Yes ☐ No \
Contains only one speaker and no identifiers? ☐ Yes ☐ No \
Participant confirmed continuation immediately before recording? ☐ Yes ☐ No \
File checksum (if used): ____________________ \
Provider/product/model/version/settings: ____________________  Account setting evidence ID: ____________________ \
Private voice alias: ____________________  Upload result: ____________________ \
Fixed prompt manifest/generation log: ____________________  Copied to headset: ☐ Yes ☐ No \
Headset provider credential/API call absent: ☐ Yes ☐ No \

If quality fails, do not retain failed takes beyond the approved immediate cleanup step. **[OPEN DECISION: maximum retry count and failure/fallback rule.]**
