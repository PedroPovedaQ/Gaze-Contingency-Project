"""Versioned MiniLM classifiers; no generated text or direct playback authority."""
import time
import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer
from data import LABELS, blocked, text, unpack
from continuation_data import model_text
import adaptive_data

MAX_TOKENS = 384


def sync(device):
    if device == 'mps': torch.mps.synchronize()


class Policy:
    def __init__(self, path, device='cpu'):
        self.device = device
        torch.set_num_threads(4)
        start = time.perf_counter()
        self.tokenizer = AutoTokenizer.from_pretrained(path, local_files_only=True)
        self.model = AutoModelForSequenceClassification.from_pretrained(path, local_files_only=True).to(device).eval()
        self.feature_version = getattr(self.model.config, 'local_jev_feature_version', None)
        self.adaptive = self.feature_version in (adaptive_data.LEGACY_FEATURE_VERSION, adaptive_data.FEATURE_VERSION)
        self.scope = 'assistance' if self.adaptive else 'navigation_action'
        self.labels = adaptive_data.LABELS if self.adaptive else LABELS
        if self.adaptive and getattr(self.model.config, 'local_jev_policy', None) != adaptive_data.POLICY:
            raise ValueError('Checkpoint assistance policy does not match the adapter')
        if [self.model.config.id2label[i] for i in range(len(self.labels))] != self.labels:
            raise ValueError('Checkpoint label mapping does not match its contract')
        sync(device)
        self.load_ms = (time.perf_counter() - start) * 1000

    def predict(self, payload, guard=True):
        start = time.perf_counter()
        adapter = adaptive_data if self.adaptive else None
        _, _, _, _, choices = (adapter.unpack if adapter else unpack)(payload)
        if guard and (adapter.blocked if adapter else blocked)(payload):
            silence = 'observe_1s' if self.adaptive else 'wait'
            return self.answer(silence, {silence: 1.0}, payload, start, 'local_gate')
        features = adapter.text(payload, getattr(self, 'feature_version', adaptive_data.FEATURE_VERSION)) if adapter else model_text(payload, self.feature_version)
        encoded = self.tokenizer(features, return_tensors='pt', truncation=False)
        if encoded['input_ids'].shape[1] > MAX_TOKENS:
            raise ValueError('Input exceeds supported context; refusing silent truncation')
        with torch.inference_mode():
            logits = self.model(**{k: v.to(self.device) for k, v in encoded.items()}).logits[0].cpu().numpy()
        if not np.isfinite(logits).all():
            raise ValueError('Nonfinite model output')
        raw = self.labels[int(logits.argmax())]
        mask = np.array([k in choices for k in self.labels])
        logits[~mask] = -np.inf
        probabilities = np.exp(logits - np.max(logits)); probabilities /= probabilities.sum()
        choice = self.labels[int(probabilities.argmax())]
        result = self.answer(choice, {k: float(probabilities[i]) for i, k in enumerate(self.labels) if k in choices}, payload, start, 'model')
        result['raw_choice'] = raw
        return result

    def answer(self, choice, probabilities, payload, start, source):
        return dict(model='local-jev-minilm-experiment', decision_id=payload['state'].get('decision_id'),
                    answers={self.scope: dict(type='choice', choice=choice, probabilities=probabilities)},
                    probability_semantics='deterministic_guard' if source == 'local_gate' else 'uncalibrated_offered_choice_softmax',
                    elapsed_ms=(time.perf_counter() - start) * 1000, source=source)
