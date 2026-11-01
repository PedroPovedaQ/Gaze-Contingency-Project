"""Offline safety tests. Never use credentials, microphone, network or participant files."""
import json
import io
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import urllib.request
import urllib.error
import wave
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import MagicMock

import participant_intake as intake
from e2e_live import LiveProvider, require_development, API


class LiveBoundaryChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='FMV-DEVELOPMENT-BOUNDARY-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.provider = LiveProvider(self.root/'receipt.json')
        self.provider.keys_path=self.root/'fake-keys.json'
        self.provider.keys_path.write_text('{"elevenlabs_key":"synthetic-key"}')
        self.folder = self.root/'sessions'/'development'
        intake.create(self.folder,'P900000000001','A',development=True)
        intake.record_consent(self.folder,'TEST','synthetic test',True,True)
        self.provider.root = self.root
        self.provider.active_folder = self.folder

    def request(self,path,method='POST'):
        return urllib.request.Request(API+path, data=b'{}' if method=='POST' else None,method=method)

    def test_live_cli_requires_development_session_before_any_network(self):
        result = subprocess.run(['python3',str(Path(__file__).with_name('e2e_intake.py')),
            '--development-audio','--live-elevenlabs','--receipts',str(self.root/'unused')],capture_output=True,text=True)
        self.assertEqual(result.returncode,2)
        self.assertIn('requires --development-session',result.stderr)
        self.assertFalse((self.root/'unused').exists())

    def test_non_development_and_outside_root_rejected(self):
        self.assertTrue(require_development(self.folder,self.root)['development'])
        intake.patch(self.folder,development=False)
        with self.assertRaises(ValueError): require_development(self.folder,self.root)
        intake.patch(self.folder,development=True,participantCode='P001')
        with self.assertRaises(ValueError): require_development(self.folder,self.root)
        intake.patch(self.folder,participantCode='P900000000001')
        with self.assertRaises(ValueError): require_development(self.folder,self.root/'other')

    def test_known_unavailable_slots_stop_before_mutations(self):
        for slots, remaining in [(0,30000)]:
            before=dict(cloneSlotsFree=slots,charactersRemaining=remaining,canUseInstantCloning=True)
            with patch.object(self.provider,'account',return_value=(before,set())):
                with self.assertRaises(RuntimeError): self.provider.preflight()
            self.assertFalse(self.provider.preflight_ok)
            self.assertEqual(self.provider.result['cloneAttempts'],0)
        before.update(cloneSlotsFree=1,charactersRemaining=6000)
        with patch.object(self.provider,'account',return_value=(before,set())): self.provider.preflight()
        self.assertTrue(self.provider.preflight_ok)

    def test_subscription_permission_denial_continues_without_other_key(self):
        for status in (401,403):
            with self.subTest(status=status):
                response=MagicMock(status=200,headers={})
                response.__enter__.return_value=response
                response.read.return_value=b'{"voices":[]}'
                error=urllib.error.HTTPError(API+'user/subscription',status,'Forbidden',{},
                    io.BytesIO(b'{"detail":{"status":"missing_permissions","message":"Missing user_read"}}'))
                with patch.object(urllib.request,'urlopen',side_effect=[response,error]) as network:
                    with self.provider.network_guard(): self.provider.preflight()
                self.assertTrue(self.provider.preflight_ok)
                self.assertEqual(self.provider.result['before']['quotaStatus'],'unavailable')
                self.assertEqual(self.provider.result['before']['httpStatus'],status)
                self.assertEqual(network.call_count,2)

    def test_tts_limit_counts_concurrent_requests_and_failed_submissions(self):
        self.provider.preflight_ok=True
        def submit(text):
            req=self.request('text-to-speech/testvoice')
            req.data=json.dumps({'text':text}).encode()
            return urllib.request.urlopen(req)
        error=urllib.error.HTTPError(API+'text-to-speech/testvoice',429,'Too many',{},io.BytesIO(b'{}'))
        with patch.object(urllib.request,'urlopen',side_effect=error) as network:
            with self.provider.network_guard():
                with self.assertRaises(urllib.error.HTTPError): submit('x'*5990)
                def attempt(_):
                    try: submit('x'*10)
                    except Exception as failure: return type(failure).__name__
                with ThreadPoolExecutor(max_workers=2) as pool:
                    results=list(pool.map(attempt,range(2)))
                self.assertCountEqual(results,['HTTPError','RuntimeError'])
                with self.assertRaises(RuntimeError): submit('x')
            self.assertEqual(network.call_count,2)
        self.assertEqual(self.provider.result['ttsCharactersSubmitted'],6000)

    def test_audio_usage_and_clone_refusal_are_recorded_without_deleting_other_voices(self):
        self.provider.preflight_ok=True
        data=io.BytesIO()
        with wave.open(data,'wb') as wav:
            wav.setnchannels(1); wav.setsampwidth(2); wav.setframerate(24000)
            wav.writeframes(b'\0\0'*30000)
        error=urllib.error.HTTPError(API+'voices/add',403,'Forbidden',{},io.BytesIO(
            b'{"detail":{"status":"voice_limit_reached","message":"No free slot"}}'))
        response=MagicMock(status=200,headers={})
        response.__enter__.return_value=response
        response.read.return_value=b'{"text":"Synthetic answer"}'
        self.provider.text='Synthetic answer'
        with patch.object(urllib.request,'urlopen',side_effect=[response,error]) as network:
            with self.provider.network_guard():
                self.provider.transcribe(data.getvalue())
                with self.assertRaises(RuntimeError): self.provider.clone('P900000000001',data.getvalue())
                with self.assertRaises(RuntimeError): self.provider.clone('P900000000001',data.getvalue())
        self.assertEqual(network.call_count,2)
        self.assertEqual(self.provider.result['scribeAudioSeconds'],1.25)
        self.assertEqual(self.provider.result['cloneSampleSeconds'],1.25)
        self.assertEqual(self.provider.result['calls'][-1]['status'],403)
        self.assertEqual(self.provider.result['calls'][-1]['reason']['message'],'No free slot')
        self.assertEqual(self.provider.result['clones'],[])

    def test_account_counts_nonpremade_conservatively(self):
        voices={'voices':[{'voice_id':'one','category':'premade'},{'voice_id':'two','category':'cloned'}]}
        quota=dict(character_count=100,character_limit=50000,voice_limit=3,can_use_instant_voice_cloning=True)
        with patch.object(self.provider,'call',side_effect=[json.dumps(voices),json.dumps(quota)]):
            state,ids=self.provider.account()
        self.assertEqual(state['cloneSlotsFree'],2)
        self.assertEqual(state['charactersRemaining'],49900)
        self.assertNotIn('two',json.dumps(state))

    def test_network_fails_closed_before_preflight_and_for_other_recipient(self):
        with patch.object(urllib.request,'urlopen') as network:
            with self.provider.network_guard():
                with self.assertRaises(ValueError): urllib.request.urlopen(self.request('voices/add'))
                with self.assertRaises(ValueError): urllib.request.urlopen(urllib.request.Request('https://example.com'))
            network.assert_not_called()

    def test_delete_cannot_target_another_voice(self):
        self.provider.preflight_ok=True
        with self.assertRaises(ValueError): self.provider.delete_voice('unrelated_voice')
        with patch.object(urllib.request,'urlopen') as network:
            with self.provider.network_guard():
                with self.assertRaises(ValueError): urllib.request.urlopen(self.request('voices/unrelated_voice','DELETE'))
            network.assert_not_called()

    def test_one_clone_attempt_and_verification_id_survives_error(self):
        with patch.object(self.provider,'multipart',return_value=dict(voice_id='development_clone',requires_verification=True)) as send:
            with self.assertRaises(intake.CloneVerificationRequired): self.provider.clone('P900000000001',b'synthetic')
            with self.assertRaises(RuntimeError): self.provider.clone('P900000000001',b'synthetic')
        self.assertEqual(send.call_count,1)
        saved=json.loads(self.provider.path.read_text())
        self.assertEqual(saved['clones'][0]['voiceId'],'development_clone')
        self.assertTrue(saved['clones'][0]['name'].startswith('DEV-E2E-'))

    def test_unknown_clone_outcome_not_retried(self):
        with patch.object(self.provider,'multipart',side_effect=RuntimeError('connection lost')) as send:
            with self.assertRaises(RuntimeError): self.provider.clone('P900000000001',b'synthetic')
            with self.assertRaises(RuntimeError): self.provider.clone('P900000000001',b'synthetic')
        self.assertEqual(send.call_count,1)
        self.assertEqual(json.loads(self.provider.path.read_text())['cloneAttempts'],1)

    def test_cleanup_verifies_exact_id_absent(self):
        clone=dict(voiceId='development_clone',deletionVerified=False)
        self.provider.result['clones']=[clone]
        with patch.object(self.provider,'delete_voice') as delete, patch.object(self.provider,'call',return_value=b'{"voices":[]}'), patch.object(self.provider,'account',side_effect=RuntimeError('quota permission missing')):
            self.provider.cleanup()
        delete.assert_called_once_with('development_clone')
        self.assertTrue(clone['deletionVerified'])
        self.assertIn('afterError',self.provider.result)

    def test_cleanup_failure_retains_id_and_never_claims_success(self):
        clone=dict(voiceId='development_clone',deletionVerified=False)
        self.provider.result['clones']=[clone]
        with patch.object(self.provider,'delete_voice',side_effect=RuntimeError('offline')), patch.object(self.provider,'account',side_effect=RuntimeError('offline')):
            with self.assertRaises(RuntimeError): self.provider.cleanup()
        saved=json.loads(self.provider.path.read_text())
        self.assertFalse(saved['clones'][0]['deletionVerified'])
        self.assertEqual(saved['clones'][0]['voiceId'],'development_clone')

    def test_receipt_cannot_be_overwritten_for_accidental_rerun(self):
        with self.assertRaises(ValueError): LiveProvider(self.provider.path)


if __name__=='__main__': unittest.main()
