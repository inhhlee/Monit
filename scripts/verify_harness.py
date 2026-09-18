"""Validate tracker structure and evidence links; never certify product behavior."""
import argparse
import json
import sys
from datetime import datetime
from pathlib import Path, PureWindowsPath

ROOT = Path(__file__).resolve().parents[1]
STATUSES = {'not_started', 'in_progress', 'blocked', 'passing'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def timestamp(value):
    result = datetime.fromisoformat(value)
    require(result.utcoffset() is not None, 'timestamp must include timezone')
    return result


def nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def validate(data, root):
    root = Path(root).resolve()
    features = data['features']
    require(isinstance(features, list) and features, 'features must be a nonempty list')
    ids = [f['id'] for f in features]
    require(all(nonempty(i) for i in ids) and len(set(ids)) == len(ids), 'duplicate/invalid feature ID')
    by_id = dict(zip(ids, features))
    require(set(data['scope']['included']) == set(ids), 'scope differs from feature IDs')
    require(sum(f['status'] == 'in_progress' for f in features) <= 1, 'multiple active features')
    visited, visiting = set(), set()

    def visit(fid):
        require(fid in by_id, f'unknown dependency {fid}')
        require(fid not in visiting, f'dependency cycle at {fid}')
        if fid in visited:
            return
        visiting.add(fid)
        for dep in by_id[fid]['depends_on']:
            visit(dep)
        visiting.remove(fid)
        visited.add(fid)

    for fid in ids:
        visit(fid)
    for f in features:
        fid = f['id']
        require(f['status'] in STATUSES, f'{fid}: invalid status')
        require(type(f['priority']) is int and f['priority'] > 0, f'{fid}: invalid priority')
        revision = f['spec_revision']
        require(type(revision) is int and revision > 0, f'{fid}: invalid revision')
        checks = f['verification']
        require(isinstance(checks, list) and checks and all(nonempty(x) for x in checks), f'{fid}: empty verification')
        if f['status'] == 'blocked':
            require(isinstance(f['blocker'], dict) and all(nonempty(f['blocker'].get(k)) for k in ('reason', 'impact', 'resolution_condition')), f'{fid}: incomplete blocker')
        else:
            require(f.get('blocker') is None, f'{fid}: stale blocker')
        if f['status'] in {'in_progress', 'passing'}:
            require(all(by_id[d]['status'] == 'passing' for d in f['depends_on']), f'{fid}: dependency not passing')
        require(isinstance(f['evidence'], list), f'{fid}: evidence must be a list')
        latest = {}
        previous_time = None
        for e in f['evidence']:
            rev, index = e['spec_revision'], e['verification_index']
            require(type(rev) is int and 1 <= rev <= revision, f'{fid}: invalid evidence revision')
            require(type(index) is int and index >= 1, f'{fid}: invalid verification index')
            if rev == revision:
                require(index <= len(checks), f'{fid}: verification index out of range')
            when = timestamp(e['executed_at'])
            require(previous_time is None or when >= previous_time, f'{fid}: evidence not chronological')
            previous_time = when
            require(e['result'] in {'passed', 'failed', 'not_run'}, f'{fid}: invalid evidence result')
            for key in ('command_or_procedure', 'expected', 'actual'):
                require(nonempty(e[key]), f'{fid}: missing {key}')
            require(bool(e['environment']) and isinstance(e['environment'], (str, dict)), f'{fid}: missing environment')
            paths = e['artifact_paths']
            require(isinstance(paths, list) and paths, f'{fid}: missing artifacts')
            for name in paths:
                require(nonempty(name) and not Path(name).is_absolute() and not PureWindowsPath(name).drive and not PureWindowsPath(name).root, f'{fid}: artifact must be relative')
                path = (root / name.replace('\\', '/')).resolve()
                require(path.is_relative_to(root) and path.is_file(), f'{fid}: missing/escaping artifact {name}')
            if rev == revision:
                latest[index] = e['result']
        if f['status'] == 'passing':
            require(all(latest.get(i) == 'passed' for i in range(1, len(checks) + 1)), f'{fid}: missing latest passing evidence')
    notes = data['design_notes']
    require(len({n['id'] for n in notes}) == len(notes), 'duplicate design note ID')
    for note in notes:
        require(note['status'] in {'unresolved', 'proposed', 'adopted', 'superseded'}, 'invalid design note status')
        require(all(fid in by_id for fid in note['affects']), 'unknown affected feature')
        if note['status'] == 'adopted':
            require(note['adoption'] in {'provisional', 'confirmed'}, 'missing adoption type')
            for key in ('decision', 'reason', 'decided_at', 'review_trigger'):
                require(nonempty(note[key]), f'missing decision field {key}')
    require(isinstance(data['change_log'], list), 'missing change log')
    return len(features), sum(len(f['verification']) for f in features)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tracker', type=Path, default=ROOT / 'feature_list.json')
    args = parser.parse_args()
    try:
        data = json.loads(args.tracker.read_text(encoding='utf-8-sig'))
        count, checks = validate(data, args.tracker.resolve().parent)
        print(f'PASS: {count} features, {checks} verification items; structure and artifact links only')
        return 0
    except (OSError, ValueError, KeyError, TypeError, AttributeError, RecursionError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
