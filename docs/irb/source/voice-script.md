# Standardized Voice Recording Script

**Current protocol decision:** Use this script only after UCF approves the offline local-processing path, exact script, workstation, and deletion procedure.

Study: **Follow My Voice: Gaze-Contingent XR Search with a Self-Similar Agent**

## Researcher Setup

- Confirm signed consent and voice-cloning authorization before recording.
- Use a private, quiet room, the approved microphone/settings, and the institution-managed encrypted voice workstation.
- Name the file only with study ID, session/run, and recording version; never use a name or email.
- Confirm that the approved OpenVoice V2 release/commit and model weights are installed on the UCF-managed encrypted workstation and that network access is disabled during participant processing.
- Confirm that temporary folders do not synchronize to consumer/cloud backup and that no cloud voice provider credential or API path is present on the workstation workflow or headset.
- Ask the participant to speak naturally at a consistent volume. Do not coach accent, identity, emotion, or impersonation.
- Record only the approved text. Stop if the participant includes a name or other personal information; delete that take and restart.
- Target approximately 1–2 minutes of clear single-speaker audio. Generate only the approved fixed prompt manifest locally on the staff workstation and copy prepared clips to the headset. Do not upload participant audio, embeddings, prompts, or generated clips to an external service.

## Participant Introduction

“I will ask you to read a short neutral passage in your normal speaking voice. Please do not say your name or add personal details. Offline software on a UCF-managed research computer will use this sample to make a private synthetic voice for this study. The synthetic voice can say the study’s fixed phrases even though you did not record those exact phrases. Your voice will not be sent to an outside voice service. You may stop before or during recording. If you stop, the recording will be deleted and you will not continue in the main experiment, with no penalty. You will still receive the $15 gift card.”

Confirm: “Do you want to continue with the recording?”  ☐ Yes ☐ No

## Recording Passage

Today I am taking part in a visual search activity. While seated, I will turn to look at several areas arranged around me and compare objects by color and shape. Some objects may look similar, so I will take my time and check each area carefully.

The assistant may offer a short hint. A hint can tell me that I am closer, farther away, or looking in a useful direction. I can listen to the hint, keep searching, and make my own choice. If I need a break, I can pause the activity.

Clear speech can change with pace, emphasis, and punctuation. I can say, “Look this way,” “colder,” “warmer,” or “very close.” I can ask, “Is the blue shape beside the red one?” I can count: one, two, three, four, five, six, seven, eight, nine, ten.

The quick brown fox moves past the quiet garden. Bright yellow stars appear above purple cubes. Round spheres, narrow cylinders, and small pyramids fill the display. Each sentence gives the system a different pattern of sounds while keeping the content neutral.

I understand that this recording is for the approved research study. I will not say my name, address, phone number, or any other personal information in this recording.

## Quality and Local Processing Record

Study ID: ____________________  Recording version/take: ____________________ \
Start/end time: ____________________  Duration: ____________________ \
Approved microphone/settings: ____________________ \
Background noise/reverb acceptable? ☐ Yes ☐ No \
Contains only one speaker and no identifiers? ☐ Yes ☐ No \
Participant confirmed continuation immediately before recording? ☐ Yes ☐ No \
File checksum (if used): ____________________ \
OpenVoice release/commit, model weights, base TTS, and settings: ____________________ \
Local speaker-embedding alias: ____________________  Offline/network check: ☐ Pass ☐ Fail \
Fixed prompt manifest/generation log: ____________________  Copied to headset: ☐ Yes ☐ No \
External voice upload/API call absent: ☐ Yes ☐ No \

Allow at most two recording takes and one regeneration of the fixed prompt library. Delete failed takes immediately. If a complete intelligible self-similar library still cannot be produced, stop before randomized blocks, provide the full $15 gift card, delete all voice artifacts, and record the technical-stop reason. Do not use a cloud service or generic-only fallback without an approved modification.
