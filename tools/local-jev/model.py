"""Five-action MiniLM classifier; no generated text or direct playback authority."""
import time
import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer
from data import LABELS, blocked, text, unpack

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
        sync(device)
        self.load_ms = (time.perf_counter() - start) * 1000

    def predict(self, payload, guard=True):
        start = time.perf_counter()
        _, _, _, _, choices = unpack(payload)
        if guard and blocked(payload):
            return self.answer('wait', {'wait': 1.0}, payload, start, 'local_gate')
        encoded = self.tokenizer(text(payload), return_tensors='pt', truncation=False)
        if encoded['input_ids'].shape[1] > MAX_TOKENS:
            raise ValueError('Input exceeds supported context; refusing silent truncation')
        with torch.inference_mode():
            logits = self.model(**{k: v.to(self.device) for k, v in encoded.items()}).logits[0].cpu().numpy()
        raw = LABELS[int(logits.argmax())]
        mask = np.array([k in choices for k in LABELS])
        logits[~mask] = -np.inf
        probabilities = np.exp(logits - np.max(logits)); probabilities /= probabilities.sum()
        choice = LABELS[int(probabilities.argmax())]
        result = self.answer(choice, {k: float(probabilities[i]) for i, k in enumerate(LABELS) if k in choices}, payload, start, 'model')
        result['raw_choice'] = raw
        return result

    @staticmethod
    def answer(choice, probabilities, payload, start, source):
        return dict(model='local-jev-minilm-experiment', decision_id=payload['state'].get('decision_id'),
                    answers=dict(navigation_action=dict(type='choice', choice=choice, probabilities=probabilities)),
                    elapsed_ms=(time.perf_counter() - start) * 1000, source=source)
