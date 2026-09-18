import copy
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
from verify_harness import validate
from verify_test_results import verify


class HarnessTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'log.txt').write_text('Synthetic fixture only', encoding='utf-8')
        self.data = json.loads((ROOT / 'feature_list.json').read_text(encoding='utf-8-sig'))
        self.f = self.data['features'][0]
        # Fixture independent of future real feature status/evidence.
        for f in self.data['features']:
            f.update(status='not_started', evidence=[], blocker=None)
        self.entry = dict(spec_revision=self.f['spec_revision'], verification_index=1,
                          executed_at='2026-09-18T18:00:00+09:00', command_or_procedure='synthetic',
                          environment='test fixture', expected='pass', actual='pass',
                          result='passed', artifact_paths=['log.txt'])

    def make_passing(self):
        self.f['status'] = 'passing'
        self.f['evidence'] = [dict(self.entry, verification_index=i) for i in range(1, len(self.f['verification']) + 1)]

    def test_initial_structure(self):
        self.assertEqual(validate(self.data, self.root), (5, 35))

    def test_complete_evidence(self):
        self.make_passing()
        validate(self.data, self.root)

    def test_latest_failure_invalidates_pass(self):
        self.make_passing()
        self.f['evidence'].append(dict(self.entry, result='failed'))
        with self.assertRaises(ValueError): validate(self.data, self.root)

    def test_old_revision_cannot_certify(self):
        self.make_passing()
        self.f['spec_revision'] += 1
        with self.assertRaises(ValueError): validate(self.data, self.root)

    def test_reject_invalid_structures(self):
        cases = [lambda d: d['features'][0].update(status='unknown'),
                 lambda d: d['features'][0].update(status='passing'),
                 lambda d: d['features'][0].update(status='blocked'),
                 lambda d: d['features'][0].update(depends_on=['COM-04']),
                 lambda d: d['features'][0].update(depends_on=['missing']),
                 lambda d: d['features'][1].update(status='in_progress'),
                 lambda d: [f.update(status='in_progress') for f in d['features'][:2]]]
        for mutate in cases:
            with self.subTest(mutation=mutate):
                data = copy.deepcopy(self.data)
                mutate(data)
                with self.assertRaises(ValueError): validate(data, self.root)

    def test_reject_bad_evidence(self):
        for change in [dict(artifact_paths=['missing']), dict(artifact_paths=['../log.txt']),
                       dict(artifact_paths=['C:\\log.txt']), dict(verification_index=0),
                       dict(executed_at='2026-09-18T18:00:00'), dict(result='success')]:
            with self.subTest(change=change):
                self.f['evidence'] = [dict(self.entry, **change)]
                with self.assertRaises(ValueError): validate(self.data, self.root)

    def test_bad_json_cli_fails(self):
        path = self.root / 'bad.json'
        path.write_text('{', encoding='utf-8')
        run = subprocess.run([sys.executable, str(ROOT / 'scripts/verify_harness.py'), '--tracker', str(path)], capture_output=True)
        self.assertEqual(run.returncode, 1)

    def test_trx_gate(self):
        path = self.root / 'test.trx'
        for total, executed, passed, outcome, valid in [(1,1,1,'Passed',True), (0,0,0,'Passed',False), (1,0,0,'NotExecuted',False), (1,1,0,'Failed',False), (1,1,1,'Failed',False)]:
            path.write_text(f'<TestRun><ResultSummary><Counters total="{total}" executed="{executed}" passed="{passed}"/></ResultSummary><Results><UnitTestResult outcome="{outcome}"/></Results></TestRun>', encoding='utf-8')
            if valid: self.assertEqual(verify(path), 1)
            else:
                with self.assertRaises(ValueError): verify(path)


if __name__ == '__main__':
    unittest.main()
