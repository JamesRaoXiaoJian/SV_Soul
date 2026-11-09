"""Queue one in-game test action via SSH. Game must load a save first."""
import argparse,json,subprocess,uuid
p=argparse.ArgumentParser();p.add_argument('action',choices=['open','send','close','inspect','controller','controls']);p.add_argument('--npc',default='Abigail');p.add_argument('--text',default='');a=p.parse_args()
r={'Id':str(uuid.uuid4()),'Action':a.action,'Npc':a.npc,'Text':a.text}
code='''import pathlib,json,os
p=pathlib.Path.home()/'Library/Application Support/Steam/steamapps/common/Stardew Valley/Contents/MacOS/Mods/SV_SOUL/test-request.json'
if p.exists(): raise SystemExit('A test request is already pending.')
r=json.loads(%r)
t=p.with_suffix('.tmp');t.write_text(json.dumps(r,ensure_ascii=False));os.replace(t,p)
print('Queued '+r['Id'])
'''%json.dumps(r)
subprocess.run(['ssh','macbook','python3','-'],input=code,text=True,check=True)
