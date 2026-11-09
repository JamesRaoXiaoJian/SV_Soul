"""Live multi-turn and memory-format checks; uses an isolated synthetic conversation."""
import json,sys,urllib.request,subprocess
from pathlib import Path
c=json.loads(Path(sys.argv[1]).read_text())
b=(c.get('BaseUrl') or 'https://api.openai.com/v1').rstrip('/')
u=b if b.endswith('/chat/completions') else b+('' if b.endswith('/v1') else '/v1')+'/chat/completions'
def call(messages,tokens=200):
 body={'model':c['Model'],'messages':messages,('max_completion_tokens' if c.get('UseMaxCompletionTokens',True) else 'max_tokens'):tokens}
 if c.get('SendTemperature',True):body['temperature']=c.get('Temperature',0.9)
 settings='url = '+json.dumps(u)+'\nheader = '+json.dumps('Authorization: Bearer '+c['ApiKey'])+'\nheader = "Content-Type: application/json"\ndata = '+json.dumps(json.dumps(body))+'\n'
 result=subprocess.run(['curl','--silent','--show-error','--fail-with-body','--max-time','60','--config','-'],input=settings,text=True,capture_output=True)
 if result.returncode: raise RuntimeError('HTTP/transport check failed: '+result.stderr[:200])
 return json.loads(result.stdout)['choices'][0]['message']['content']

m=[{'role':'system','content':'你是星露谷的Abigail。用简短中文回应玩家，记住玩家告诉你的喜好。'},{'role':'user','content':'你好，我喜欢钓鱼。我的测试代号是蓝莓47。'}]
a=call(m); assert any('\u4e00'<=x<='\u9fff' for x in a), 'No Chinese reply'
m += [{'role':'assistant','content':a},{'role':'user','content':'我刚才说我喜欢什么？测试代号是什么？'}]
b=call(m); assert '钓鱼' in b and '蓝莓47' in b, 'Multi-turn recall failed'
facts=call([{'role':'system','content':''},{'role':'user','content':'Extract facts from: Player likes fishing; test code is 蓝莓47. Return ONLY a JSON array of short string facts.'}],300)
f=json.loads(facts); assert isinstance(f,list) and f and all(isinstance(x,str) for x in f), 'Invalid memory format'
print(json.dumps({'chinese_reply':a,'recall_reply':b,'memory_facts':f,'result':'PASS'},ensure_ascii=False))
