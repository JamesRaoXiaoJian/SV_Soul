"""Queue an in-game test action on the SSH alias home (no keyboard required)."""
import argparse,base64,json,subprocess,uuid
p=argparse.ArgumentParser();p.add_argument('action',choices=['open','send','close','inspect','controller','controls']);p.add_argument('--npc',default='Abigail');p.add_argument('--text',default='');a=p.parse_args()
id=str(uuid.uuid4());payload=base64.b64encode(json.dumps({'Id':id,'Action':a.action,'Npc':a.npc,'Text':a.text},ensure_ascii=False).encode()).decode()
script=r"""$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $ErrorActionPreference='Stop'; $p='G:\SteamLibrary\steamapps\common\Stardew Valley\Mods\SV_SOUL\test-request.json'; if(Test-Path $p){throw 'A request is already pending'}; $json=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('PAYLOAD')); [IO.File]::WriteAllText($p+'.tmp',$json,(New-Object Text.UTF8Encoding($false))); Move-Item ($p+'.tmp') $p; Write-Output 'Queued ID'""".replace('PAYLOAD',payload).replace('Queued ID','Queued '+id)
raise SystemExit(subprocess.run(['ssh','home','powershell','-NoProfile','-EncodedCommand',base64.b64encode(script.encode('utf-16le')).decode()]).returncode)
