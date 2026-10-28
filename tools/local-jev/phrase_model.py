"""MiniLM scores every candidate jointly with the state; no runtime teacher rules."""
import time
import numpy as np
import torch
from transformers import AutoModelForSequenceClassification, AutoTokenizer
import adaptive_data
import phrase_data


class PhrasePolicy:
    def __init__(self, path, device='cpu'):
        self.device = device
        self.tokenizer = AutoTokenizer.from_pretrained(path, local_files_only=True)
        self.model = AutoModelForSequenceClassification.from_pretrained(path, local_files_only=True).to(device).eval()
        if getattr(self.model.config, 'phrase_feature_version', None) != phrase_data.VERSION or self.model.config.num_labels != 1:
            raise ValueError('Phrase checkpoint contract mismatch')

    def predict(self, request):
        start = time.perf_counter()
        state, choices, _, action_request = phrase_data.unpack(request)
        if adaptive_data.blocked(action_request, allow_playback=phrase_data.wall_arrival_override(state, choices)):
            raise ValueError('Phrase request no longer permits speech')
        encoded = self.tokenizer(phrase_data.features(request), padding=True, truncation=False, return_tensors='pt')
        if encoded['input_ids'].shape[1] > 384: raise ValueError('Refusing phrase context truncation')
        with torch.inference_mode():
            scores = self.model(**{k:v.to(self.device) for k,v in encoded.items()}).logits[:,0].cpu().numpy()
        if not np.isfinite(scores).all(): raise ValueError('Nonfinite phrase scores')
        probabilities = np.exp(scores - scores.max()); probabilities /= probabilities.sum()
        phrases = list(choices); choice = phrases[int(scores.argmax())]
        return dict(model='local-jev-minilm-phrase', decision_id=state.get('decision_id'),
                    answers={phrase_data.SCOPE:dict(type='choice', choice=choice,
                              probabilities={p:float(probabilities[i]) for i,p in enumerate(phrases)})},
                    probability_semantics='uncalibrated_offered_choice_softmax', source='phrase_model',
                    elapsed_ms=(time.perf_counter()-start)*1000)
