"""Endpoint boundaries tested without loading weights or contacting a provider."""
from http.server import HTTPServer
import json
import threading
import unittest
import requests
from data import unpack
from serve import handler


class StubPolicy:
    device = 'test'

    def predict(self, payload):
        unpack(payload)
        return {'answers': {'navigation_action': {'type': 'choice', 'choice': 'wait'}}}


class ServerChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = HTTPServer(('127.0.0.1', 0), handler(StubPolicy()))
        cls.worker = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.worker.start()
        cls.url = f'http://127.0.0.1:{cls.server.server_port}'

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.worker.join(timeout=5)

    def test_health_and_unknown_route(self):
        self.assertEqual(requests.get(self.url+'/health', timeout=2).json()['scope'], 'navigation_action')
        self.assertEqual(requests.get(self.url+'/missing', timeout=2).status_code, 404)

    def test_bad_body_shapes_are_rejected_and_server_survives(self):
        for body in ('{', '[]', 'null', '{"questions":null}', '{"questions":{"navigation_action":null}}'):
            response = requests.post(self.url+'/typesafe/v1/systemone', data=body, timeout=2)
            self.assertEqual(response.status_code, 400)
            self.assertEqual(response.json()['error'], 'unsupported_or_invalid_action_request')
        self.assertEqual(requests.get(self.url+'/health', timeout=2).status_code, 200)

    def test_large_body_is_rejected(self):
        response = requests.post(self.url+'/typesafe/v1/systemone', data='x'*65537, timeout=2)
        self.assertEqual(response.status_code, 413)


if __name__ == '__main__':
    unittest.main()
