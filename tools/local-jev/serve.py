"""Loopback-only experimental TypeSafe-shaped action endpoint; never starts headset trials."""
import argparse
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
from pathlib import Path
from model import Policy


def handler(policy):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args): pass  # Do not retain request state or identifiers.

        def reply(self, status, value):
            body = json.dumps(value).encode()
            self.send_response(status); self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(body))); self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path != '/health': return self.reply(404, {'error': 'not_found'})
            self.reply(200, {'status': 'ready', 'scope': 'navigation_action', 'device': policy.device})

        def do_POST(self):
            if self.path != '/typesafe/v1/systemone': return self.reply(404, {'error': 'not_found'})
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if not 0 < length <= 65536: return self.reply(413, {'error': 'invalid_body_size'})
                self.connection.settimeout(5)
                payload = json.loads(self.rfile.read(length))
                if not isinstance(payload, dict): raise ValueError('Expected object')
                result = policy.predict(payload)
                self.reply(200, result)
            except (ValueError, KeyError, TypeError, AttributeError):
                self.reply(400, {'error': 'unsupported_or_invalid_action_request'})
            except TimeoutError:
                self.reply(408, {'error': 'request_timeout'})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--device', choices=['cpu', 'mps'], default='cpu')
    parser.add_argument('--port', type=int, default=8766)
    args = parser.parse_args()
    policy = Policy(args.model, args.device)
    server = HTTPServer(('127.0.0.1', args.port), handler(policy))
    print(json.dumps(dict(status='listening',host='127.0.0.1',port=args.port,load_ms=policy.load_ms)),flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()


if __name__ == '__main__': main()
