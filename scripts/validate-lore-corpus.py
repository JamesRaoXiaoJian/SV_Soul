"""Validate packaged lore coverage and emit a reproducible source inventory. No game or API needed."""
import json
from collections import Counter
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
entries=[]
for file in sorted((ROOT/'assets/lore').glob('*.json')):
    data=json.loads(file.read_text())
    assert isinstance(data,list),file
    entries.extend(data)
ids=[e['Id'] for e in entries]
assert len(ids)==len(set(ids)), 'Duplicate IDs'
assert len({e.get('FactKey',e['Id']) for e in entries})==len(entries),'Duplicate fact keys'
for e in entries:
    assert e['Text'] and e['Aliases'] and all(isinstance(a,str) and a for a in e['Aliases']),e['Id']
    assert e['Source'].startswith(('https://stardewvalleywiki.com/','game:')),e['Id']
    assert e.get('GameVersion')=='1.6' and e.get('ReviewedAt') and e.get('Locale'),e['Id']
profiles={json.loads(p.read_text())['Name'] for p in (ROOT/'assets/personalities').glob('*.json')}
covered={entity for e in entries if e.get('Kind')=='character' for entity in e['EntityIds']}
assert profiles<=covered, sorted(profiles-covered)
festivals={entity for e in entries if e.get('Kind')=='festival' for entity in e['EntityIds']}
assert len(festivals)==12,festivals
cases=json.loads((ROOT/'tests/fixtures/lore-queries.json').read_text())
for case in cases:
    for id in case.get('ExpectedIds',[]):
        assert id in ids or id.startswith(('calendar.','object.','birthday.','family.')),id
manifest={
 'gameSeries':'1.6','schemaVersion':1,'packagedEntries':len(entries),
 'characterProfiles':len(profiles),'characterCoverage':sorted(covered),'festivalCoverage':sorted(festivals),
 'kinds':dict(sorted(Counter(e.get('Kind','background') for e in entries).items())),
 'sourceInventory':[{'source':source,'ids':[e['Id'] for e in entries if e['Source']==source]} for source in sorted({e['Source'] for e in entries})],
 'runtimeSources':['Data/Characters','Data/Objects','Data/Festivals/FestivalDates','Data/Festivals/<key>','Data/PassiveFestivals','localized Strings assets'],
 'unknownCanon':['??? has no confirmed real identity; retain unknown rather than inventing a biography.'],
 'excludedByDesign':['Unreviewed event scripts and alternate outcomes','Quest solutions and hidden item descriptions without appropriate knowledge','Numeric gameplay guides unrelated to NPC conversation']
}
output=ROOT/'artifacts/tests/lore-corpus-inventory.json';output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({k:v for k,v in manifest.items() if k in ('packagedEntries','characterProfiles','kinds')},ensure_ascii=False))
