"""Check the configured Chat Completions endpoint without starting the game.
Usage: python3 check-api.py /path/to/config.json
"""
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

config = json.loads(Path(sys.argv[1]).read_text())
if str(config.get('ApiProvider', '')).lower() != 'openai':
    raise SystemExit('This check requires ApiProvider=OpenAI.')
key = config.get('ApiKey', '')
if not key.strip():
    raise SystemExit('ApiKey is empty.')
base = (config.get('BaseUrl') or 'https://api.openai.com/v1').rstrip('/')
url = base if base.endswith('/chat/completions') else base + ('' if base.endswith('/v1') else '/v1') + '/chat/completions'
body = {'model': config['Model'], 'messages': [{'role': 'user', 'content': 'Reply with exactly OK.'}]}
body['max_completion_tokens' if config.get('UseMaxCompletionTokens', True) else 'max_tokens'] = 200
if config.get('SendTemperature', True):
    body['temperature'] = config.get('Temperature', 0.9)
request = urllib.request.Request(url, data=json.dumps(body).encode(), headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
try:
    with urllib.request.urlopen(request, timeout=60) as response:
        data = json.load(response)
        reply = data['choices'][0]['message']['content']
        if not isinstance(reply, str) or not reply.strip():
            raise ValueError('Response has no text content.')
        print(json.dumps({'status': response.status, 'model': config['Model'], 'reply': reply, 'usage': data.get('usage')}, ensure_ascii=False))
except urllib.error.HTTPError as error:
    print('HTTP', error.code, error.read().decode().replace(key, '[REDACTED]')[:1200])
    sys.exit(1)
except Exception as error:
    print(type(error).__name__, str(error).replace(key, '[REDACTED]'))
    sys.exit(1)
