# Interactive Paper Podcast

Interactive Paper Podcast is a local-first side package for listening to a PDF-derived podcast as
a conversation instead of a fixed recording. While audio is playing, the listener can interrupt,
ask a question, receive a source-grounded answer from one of the hosts, and resume playback.

Each run is persisted as JSON. The package stores:

- the playback position where each interruption occurred;
- the question, answer, speaking character, and supporting source-chunk indices;
- a rolling and end-of-run discussion summary; and
- bounded character memories that evolve each host's conversational habits over time.

The original personality remains stable. Learned memories describe interaction style—not new facts
about the paper—so a character can become more skeptical, concise, or explanatory without silently
rewriting the evidence.

## Why local models

The default answer model is Ollama at `http://127.0.0.1:11434`, using
`llama3.2:1b`. This avoids a network round trip during an interruption and keeps the paper and
conversation on the machine. The included lexical retriever selects relevant full-paper chunks
before generation, which keeps prompts bounded. An OpenAI-compatible endpoint remains available
for higher-quality remote or LAN-hosted models.

## Prepare an episode

Use an existing podcast audio file and its reviewed transcript:

```sh
./interactive-paper-podcast prepare /absolute/path/paper.pdf \
  --title "Gaze-based Prediction of Cognitive Load in Augmented Reality" \
  --transcript ../paper-to-podcast/output/nerella-2026-gaze-cognitive-load-voxtral.transcript.txt \
  --audio ../paper-to-podcast/output/nerella-2026-gaze-cognitive-load-voxtral.mp3
```

The command prints the new session ID. If transcript or audio is omitted, the session is still
questionable; the package creates a rough extractive transcript and the web player shows no audio.

## Run the player

```sh
./interactive-paper-podcast serve --open
```

Open `http://127.0.0.1:8765`. Press **Pause & ask** at any time. The browser pauses the audio
before sending the question. Answers are displayed in the discussion pane and saved immediately.
Press **Finish & summarize run** to append a durable run-summary record.

## Model configuration

| Variable | Default | Purpose |
|---|---|---|
| `IPP_OLLAMA_URL` | `http://127.0.0.1:11434` | Ollama server |
| `IPP_OLLAMA_MODEL` | `llama3.2:1b` | Fast local answer model |
| `IPP_MODEL_URL` | unset | OpenAI-compatible `/v1` base URL |
| `IPP_MODEL` | `gpt-5-mini` | OpenAI-compatible model name |
| `OPENAI_API_KEY` | unset | Credential for a compatible remote endpoint |

Select explicitly with `--model-provider ollama` or `--model-provider openai`. `auto` uses an
OpenAI-compatible endpoint only when one is configured; otherwise it chooses Ollama.

## Tests

```sh
python3 -m unittest discover -s tests -v
```

The suite includes retrieval, persistence, character evolution, host alternation, and a real
threaded-HTTP end-to-end flow covering health, interruption, answer persistence, reload, and final
run summarization. No external model is required for tests.

## Current product boundary

The fixed episode retains its produced host voices. Live answers are displayed immediately and,
by default, read aloud with a distinct local browser voice per character. This avoids a second
network call in the interruption path. The voice checkbox can disable speech, and **Resume episode**
cancels any live answer speech before restarting the recording. Browser microphone capture is
intentionally not required—the user can type immediately, which makes the first version reliable
across browsers and headsets.
