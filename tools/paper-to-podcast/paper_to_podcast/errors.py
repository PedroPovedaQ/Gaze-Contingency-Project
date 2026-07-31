"""Typed errors surfaced by the command-line interface."""


class PaperToPodcastError(RuntimeError):
    """Base error with a stable machine-readable code."""

    code = "PAPER_TO_PODCAST_ERROR"

    def __init__(self, message: str):
        super().__init__(message)


class PdfExtractionError(PaperToPodcastError):
    code = "PDF_EXTRACTION_ERROR"


class ScriptGenerationError(PaperToPodcastError):
    code = "SCRIPT_GENERATION_ERROR"


class SpeechSynthesisError(PaperToPodcastError):
    code = "SPEECH_SYNTHESIS_ERROR"


class AudioValidationError(PaperToPodcastError):
    code = "AUDIO_VALIDATION_ERROR"
