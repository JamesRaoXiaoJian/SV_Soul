"""Run paired offline character responses. Config/key stay on the executing machine."""
import json,subprocess,sys,time
from pathlib import Path
config=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
base=(config.get('BaseUrl') or 'https://api.openai.com/v1').rstrip('/')
url=base if base.endswith('/chat/completions') else base+('' if base.endswith('/v1') else '/v1')+'/chat/completions'
results=[]
variants=[('profiles',sys.argv[3])] if sys.argv[2]=='--single' else [('baseline',sys.argv[2]),('candidate',sys.argv[3])]
for variant,path in variants:
 for case in json.loads(Path(path).read_text(encoding="utf-8")):
  body={'model':config['Model'],'messages':[{'role':'system','content':case['system']},{'role':'user','content':case['user']}],('max_completion_tokens' if config.get('UseMaxCompletionTokens',True) else 'max_tokens'):config.get('MaxTokens',200)}
  if config.get('SendTemperature',True):body['temperature']=config.get('Temperature',0.9)
  cfg='url = '+json.dumps(url)+'\nheader = '+json.dumps('Authorization: Bearer '+config['ApiKey'])+'\nheader = "Content-Type: application/json"\ndata = '+json.dumps(json.dumps(body))+'\n'
  start=time.monotonic()
  r=subprocess.run(['curl','--silent','--show-error','--fail-with-body','--max-time','60','--config','-'],input=cfg,text=True,encoding="utf-8",capture_output=True)
  row={'variant':variant,'npc':case.get('npc'),'relationship':case.get('relationship'),'user':case['user'],'seconds':round(time.monotonic()-start,2)}
  if r.returncode:row['error']='transport/http failure '+str(r.returncode)
  else:
   try:
    d=json.loads(r.stdout);row['reply']=d['choices'][0]['message']['content'];row['usage']=d.get('usage')
   except (ValueError,KeyError,IndexError):row['error']='invalid response'
  results.append(row);Path(sys.argv[4]).write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding="utf-8");print(variant,len(results),'done',flush=True)
