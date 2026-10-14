"""Aggregate Dealbreak telemetry; Python 3 standard library only. Not a simulator."""
import argparse
import csv
import json
from collections import Counter, defaultdict
from pathlib import Path
from statistics import mean


def analyze(journals):
    runs, encounters = [], []
    seen = set()
    for journal in journals:
        if journal.get('schemaVersion') != 1:
            raise ValueError('Unsupported telemetry schema')
        if journal['id'] in seen:
            raise ValueError('Duplicate run id: ' + journal['id'])
        seen.add(journal['id'])
        events = journal['events']
        credits = [e['a'] for e in events if e['kind'] == 'credits']
        lethal = [e['detail'] for e in events if e['kind'] in ('lethal', 'invalid')]
        eligible = not any(journal.get(flag) for flag in ('truncated', 'developmentModified', 'invalid'))
        runs.append(dict(id=journal['id'], seed=journal['seed'], outcome=journal['outcome'],
                         eligible=eligible, earned=sum(max(0, n) for n in credits),
                         spent=sum(max(0, -n) for n in credits),
                         defeat_cause=lethal[-1] if lethal else ('unknown_or_forced' if journal['outcome'] == 'defeat' else '')))
        grouped = defaultdict(list)
        for event in events:
            if event['encounter'] > 0:
                grouped[event['encounter']].append(event)
        for number, entries in grouped.items():
            starts = [e for e in entries if e['kind'] == 'encounter_start']
            ends = [e for e in entries if e['kind'] == 'encounter_end']
            if not starts:
                continue
            start = starts[0]
            total = lambda kind, field='a': sum(e[field] for e in entries if e['kind'] == kind)
            count = lambda kind: sum(e['kind'] == kind for e in entries)
            block = total('block')
            capacity = total('block', 'b')
            encounters.append(dict(run_id=journal['id'], seed=journal['seed'], encounter=number,
                map=start['map'] + 1, node=start['node'], kind=start['detail'], eligible=eligible,
                outcome=ends[-1]['detail'] if ends else 'incomplete',
                seconds=ends[-1]['seconds'] - start['seconds'] if ends else '',
                turns=count('player_turn'), attacks=count('attack_action'),
                damage_dealt=total('damage_dealt'), instant_defeats=count('instant_defeat'), damage_received=total('damage_received'),
                shield_prevented=total('damage_received', 'c'), modifier_prevented=total('damage_received', 'd'),
                blocked=block, block_capacity=capacity, block_efficiency=block / capacity if capacity else '',
                tricks=count('trick_used'),
                enemies='|'.join(e['detail'] for e in entries if e['kind'] == 'enemy'),
                gear='|'.join(e['detail'] for e in entries if e['kind'] == 'gear_exposure')))
    valid_runs = [r for r in runs if r['eligible'] and r['outcome'] in ('completed', 'defeat')]
    valid_encounters = [e for e in encounters if e['eligible'] and e['outcome'] in ('Victory', 'Defeat')]
    groups = defaultdict(list)
    for encounter in valid_encounters:
        groups[(encounter['map'], encounter['kind'])].append(encounter)
    strata = []
    for (map_index, kind), rows in sorted(groups.items()):
        capacity = sum(r['block_capacity'] for r in rows)
        strata.append(dict(map=map_index, kind=kind, n=len(rows),
            defeat_rate=mean(r['outcome'] == 'Defeat' for r in rows),
            mean_hp_lost=mean(r['damage_received'] for r in rows),
            mean_turns=mean(r['turns'] for r in rows),
            mean_seconds=mean(r['seconds'] for r in rows),
            block_efficiency=sum(r['blocked'] for r in rows) / capacity if capacity else None))
    return runs, encounters, dict(runs=len(runs), terminal_eligible_runs=len(valid_runs),
        unique_terminal_seeds=len({r['seed'] for r in valid_runs}),
        completion_rate=mean(r['outcome'] == 'completed' for r in valid_runs) if valid_runs else None,
        outcomes=dict(Counter(r['outcome'] for r in runs)), strata=strata)


def write_csv(path, rows):
    if not rows:
        path.write_text('', encoding='utf-8')
        return
    with path.open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        # Neutralize spreadsheet formulas in external strings without altering source JSON.
        writer.writerows({k: "'" + v if isinstance(v, str) and v.startswith(('=', '+', '-', '@', '\t', '\r')) else v
                          for k, v in row.items()} for row in rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path, help='Folder containing run JSON journals')
    parser.add_argument('--output', type=Path, required=True, help='Separate analysis output folder')
    args = parser.parse_args()
    paths = sorted(args.input.glob('*.json'))
    if not paths:
        parser.error('No JSON journals found')
    journals = [json.loads(p.read_text(encoding='utf-8')) for p in paths]
    runs, encounters, summary = analyze(journals)
    args.output.mkdir(parents=True, exist_ok=True)
    write_csv(args.output / 'runs.csv', runs)
    write_csv(args.output / 'encounters.csv', encounters)
    hp = [dict(run_id=j['id'], **e) for j in journals for e in j['events'] if e['kind'] == 'hp']
    write_csv(args.output / 'hp.csv', hp)
    usage = [dict(run_id=j['id'], **e) for j in journals for e in j['events']
             if e['kind'] in ('gear_exposure', 'trick_used', 'credits')]
    write_csv(args.output / 'usage_and_economy.csv', usage)
    (args.output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    main()
