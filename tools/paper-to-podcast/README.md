# Paper to Podcast

This project-owned command-line tool turns a local academic PDF into:

- a complete-paper transcript;
- a validated MP3;
- JSON metadata containing source provenance, chunk coverage, providers, and audio checks; and
- optional copies for iCloud or another export folder.

It has no runtime dependency on WebKasa. PDF extraction invokes Poppler's `pdftotext` with the
absolute PDF supplied on the command line. It never imports `pdf-parse`, so it cannot trigger that
package's historical `./test/data/05-versions-space.pdf` fixture bug.

## Requirements

- Python 3.11 or newer
- Poppler (`pdftotext`)
- FFmpeg and FFprobe
- Mistral credentials for Voxtral speech, or macOS `say` for optional local fallback

No Python packages are required. On macOS, install missing media tools with:

```sh
brew install poppler ffmpeg
```

## Quick start

From this directory:

```sh
./paper-to-podcast generate /absolute/path/paper.pdf \
  --output-dir output \
  --title "Paper title"
```

With OpenAI configured, `auto` performs map/reduce script generation across every source chunk,
then synthesizes speech. When `MISTRAL_API_KEY` is configured, `auto` selects Voxtral speech
before other providers. Without external keys, script generation uses a deterministic, full-paper
extractive fallback and speech can use macOS. For evidence-sensitive research, pass a
human-reviewed transcript:

```sh
./paper-to-podcast generate /absolute/path/paper.pdf \
  --output-dir output \
  --title "Paper title" \
  --transcript-file examples/reviewed-transcript.txt \
  --tts-provider voxtral \
  --voxtral-voices alloy,echo \
  --export-dir "/Users/name/Library/Mobile Documents/com~apple~CloudDocs/Research/Project" \
  --export-all
```

The reviewed-transcript path does not bypass extraction. The tool still extracts and chunks the
complete PDF and records source SHA-256, extracted character count, chunk count, and chunk sizes.

To inspect extraction independently:

```sh
./paper-to-podcast extract /absolute/path/paper.pdf --out output/paper.txt
```

## Full-paper handling

The tool does not slice the first N characters. It divides the entire normalized extraction into
paragraph-aware bounded chunks. Oversized paragraphs are sentence-split and, if necessary,
hard-split without dropping content. The OpenAI path summarizes every chunk before synthesizing a
single transcript from all summaries. Metadata records chunk and summary counts so coverage is
auditable.

## Configuration

All provider configuration comes from the environment. Do not add keys to the repository.

| Variable | Default | Purpose |
|---|---|---|
| `OPENAI_API_KEY` | unset | Enables OpenAI scripting and TTS in `auto` mode |
| `MISTRAL_API_KEY` | unset | Enables Mistral Voxtral TTS in `auto` mode |
| `P2P_SCRIPT_PROVIDER` | `auto` | `auto`, `openai`, or `offline` |
| `P2P_TTS_PROVIDER` | `auto` | `auto`, `voxtral`, `openai`, or `macos` |
| `P2P_VOXTRAL_MODEL` | `voxtral-mini-tts-2603` | Mistral speech model |
| `P2P_VOXTRAL_VOICES` | `alloy,echo` | WebKasa aliases or direct Voxtral voice IDs |
| `P2P_MISTRAL_BASE_URL` | `https://api.mistral.ai/v1` | Mistral API base URL |
| `P2P_OPENAI_BASE_URL` | `https://api.openai.com/v1` | OpenAI-compatible base URL |
| `P2P_OPENAI_MODEL` | `gpt-5-mini` | Responses model |
| `P2P_OPENAI_TTS_MODEL` | `gpt-4o-mini-tts` | Speech model |
| `P2P_OPENAI_VOICE` | `alloy` | OpenAI voice |
| `P2P_MACOS_VOICE` | `Samantha` | macOS voice |
| `P2P_CHUNK_CHARS` | `12000` | Maximum full-paper source chunk size |
| `P2P_PDFTOTEXT` | `pdftotext` | Extraction executable |
| `P2P_FFMPEG` | `ffmpeg` | MP3 conversion/assembly executable |
| `P2P_FFPROBE` | `ffprobe` | Audio validation executable |
| `P2P_SAY` | `say` | macOS speech executable |

WebKasa-compatible defaults map `alloy` to `gb_jane_neutral` and `echo` to
`gb_oliver_neutral`. Voxtral generates bounded WAV segments, which FFmpeg assembles into the final
MP3. Metadata records the model, provider endpoint, alias-to-voice mapping, request count, input
character count, and whether a system fallback was used.

If external script generation fails in `auto` mode, the tool records the provider error and uses
the full-paper offline extractive fallback. If external TTS fails, it records that provider error and
may fall back to macOS for OpenAI speech. Voxtral failures do not fall back to a system voice.
An explicitly requested `openai` script provider fails rather than silently changing provenance.

`--env-file` loads only `MISTRAL_API_KEY`, `OPENAI_API_KEY`, and `AI_GATEWAY_API_KEY` into the
current process. Secret values are never written to metadata or copied into the repository.

## Tests

```sh
python3 -m unittest discover -s tests -v
```

Tests cover absolute-path extraction, the historical fixture-path regression, invalid/missing/image-
only PDFs, extractor failures, bounded full-text chunking, oversized blocks, offline coverage, and
reviewed-transcript errors.

Generated audio and the `output/` folder are ignored by Git. Metadata never includes API keys.
