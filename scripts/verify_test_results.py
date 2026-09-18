"""Reject absent, empty, failed or skipped VSTest runs (exit 0 alone is insufficient)."""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def verify(path):
    root = ET.parse(path).getroot()
    counters = root.find('.//{*}Counters')
    if counters is None:
        raise ValueError('Missing TRX counters')
    total = int(counters.get('total', '0'))
    results = root.findall('.//{*}UnitTestResult')
    if total < 1 or any(int(counters.get(key, '0')) != total for key in ('executed', 'passed')):
        raise ValueError('Tests must be nonempty, all executed and all passed')
    if len(results) != total or any(r.get('outcome') != 'Passed' for r in results):
        raise ValueError('TRX individual results disagree with passing counters')
    return total


if __name__ == '__main__':
    try:
        print(f'PASS: {verify(Path(sys.argv[1]))} tests executed and passed')
    except (OSError, ValueError, ET.ParseError, IndexError) as error:
        print(f'FAIL: {error}', file=sys.stderr)
        sys.exit(1)
