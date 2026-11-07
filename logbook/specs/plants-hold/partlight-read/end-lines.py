# The end and books lines of a card group's log, with the project root taken off its paths.
# Usage: python -I end-lines.py <card-run log> <out file>
import sys
root = 'D:\\Projects\\experiments\\evolution-simulator\\'
lines = open(sys.argv[1], encoding='utf-8').read().splitlines()
keep = [l.replace(root, '') for l in lines if ' ended (' in l or 'books over' in l]
open(sys.argv[2], 'w', encoding='utf-8').write('\n'.join(keep) + '\n')
print(len(keep), 'lines')
