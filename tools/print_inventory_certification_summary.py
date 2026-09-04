from pathlib import Path
import json
p=Path(__file__).resolve().parents[1]/'artifacts'/'inventory-corpus'/'physical-certification'/'corpus-physical-certification.json'
r=json.loads(p.read_text(encoding='utf-8'))
s=r['summary']
print(f"VERDICT={r['verdict']} PASSED={s['passed']} FAILED={s['failed']} PHYSICAL={s['physicalUnitCount']} CANDIDATES={s['candidateCount']} MATCHED={s['matchedPhysicalUnitCount']} UNMATCHED={s['unmatchedPhysicalUnitCount']} UNSUPPORTED={s['unsupportedCandidateCount']} DUPLICATES={s['duplicateCandidateCount']} BLOCKING={s['blockingCandidateCount']}")
print('BLOCKERS='+json.dumps(r.get('blockersByFileCount',{}),sort_keys=True))
