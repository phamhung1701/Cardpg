import json
import tempfile
import unittest
from pathlib import Path
from analyze import analyze, write_csv


def event(kind, a=0, b=0, encounter=1, detail='', seconds=0):
    return dict(kind=kind, a=a, b=b, c=0, d=0, encounter=encounter,
                map=0, node=2, detail=detail, seconds=seconds)


def journal(id='a', **overrides):
    result = dict(schemaVersion=1, id=id, seed='seed-' + id, outcome='completed', events=[
        event('encounter_start', detail='Combat', seconds=2),
        event('player_turn'), event('damage_dealt', 5, 20),
        event('block', 3, 8), event('block', 2, 2),
        event('credits', 10), event('credits', -3),
        event('encounter_end', detail='Victory', seconds=12)])
    result.update(overrides)
    return result


class AnalysisTests(unittest.TestCase):
    def test_uses_actual_hp_loss_and_weighted_block_efficiency(self):
        runs, encounters, summary = analyze([journal()])
        self.assertEqual(encounters[0]['damage_dealt'], 5)
        self.assertEqual(encounters[0]['block_efficiency'], .5)
        self.assertEqual(encounters[0]['seconds'], 10)
        self.assertEqual(runs[0]['earned'], 10)
        self.assertEqual(runs[0]['spent'], 3)
        self.assertEqual(summary['completion_rate'], 1)

    def test_excludes_modified_truncated_and_interrupted_from_completion_rate(self):
        data = [journal(), journal('b', outcome='defeat'), journal('c', outcome='interrupted'),
                journal('d', developmentModified=True), journal('e', truncated=True),
                journal('f', invalid=True)]
        _, _, summary = analyze(data)
        self.assertEqual(summary['terminal_eligible_runs'], 2)
        self.assertEqual(summary['unique_terminal_seeds'], 2)
        self.assertEqual(summary['completion_rate'], .5)

    def test_incomplete_encounter_not_in_strata(self):
        data = journal(outcome='interrupted')
        data['events'].pop()
        _, encounters, summary = analyze([data])
        self.assertEqual(encounters[0]['outcome'], 'incomplete')
        self.assertEqual(summary['strata'], [])
        self.assertIsNone(summary['completion_rate'])

    def test_rejects_duplicates_and_unknown_schema(self):
        with self.assertRaises(ValueError):
            analyze([journal(), journal()])
        with self.assertRaises(ValueError):
            analyze([journal(schemaVersion=2)])

    def test_csv_escapes_strings_and_preserves_numeric_values(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'test.csv'
            write_csv(path, [dict(seed='=SUM(1,2)', loss=-3)])
            text = path.read_text()
            self.assertIn("'=SUM(1,2)", text)
            self.assertIn('-3', text)

    def test_defeat_cause_and_no_block_are_not_fabricated(self):
        data = journal(outcome='defeat', events=[event('encounter_start', detail='Elite'),
            event('lethal', detail='EnemyAggregate'), event('encounter_end', detail='Defeat')])
        runs, encounters, _ = analyze([json.loads(json.dumps(data))])
        self.assertEqual(runs[0]['defeat_cause'], 'EnemyAggregate')
        self.assertEqual(encounters[0]['block_efficiency'], '')


if __name__ == '__main__':
    unittest.main()
