"""The v5 sheet's stills lists: hue-dials/map2/list.json with each picture moved to full-colour/<look>/png/.

    python -I make_lists.py

Looks: v5-pick, v5-wheel, v5-value1, v5-value05 (step Ӆ of slot-r62kg.ps1); v5b-c14-v05, v5b-c14-v1, v5b-c20-v05,
v5b-c20-v1 (step Ӈ); v5c-l58-v05, v5c-l50-v05, v5c-l42-v05, v5c-l50-v1 (step Ӊ); v5d-s145, v5d-s130, v5d-s115 (step Ӌ); v6-axes, v6-grey, v6-brown, v6-narrow, v6-multi-far, v6-multi-near (step Ӎ); v6b-muted, v6b-accents, v6b-darkpattern (step Ӑ); v6c-mid345, v6c-mid0, v6c-mid15 (step Ӓ); v6d-mild, v6d-mid, v6d-over (step Ӕ); v7-reef, v7-reef-pattern, v7-reef-over (step Ӝ); v7b-a, v7b-b, v7b-over (step Ӟ); v7c-family, v7c-bars, v7c-eyespot, v7c-edge (step Ӡ). Rewriting a list leaves its pictures alone. The genomes stay where hue-dials/parts/ holds them.
"""
import json
import os

S = 'D:/Projects/experiments/evolution-simulator/scratch/skin-map'
SOURCE = S + '/hue-dials/map2/list.json'
LOOKS = ['v5-pick', 'v5-wheel', 'v5-value1', 'v5-value05', 'v5b-c14-v05', 'v5b-c14-v1', 'v5b-c20-v05', 'v5b-c20-v1',
         'v5c-l58-v05', 'v5c-l50-v05', 'v5c-l42-v05', 'v5c-l50-v1',
         'v5d-s145', 'v5d-s130', 'v5d-s115',
         'v6-axes', 'v6-grey', 'v6-brown', 'v6-narrow', 'v6-multi-far', 'v6-multi-near',
         'v6b-muted', 'v6b-accents', 'v6b-darkpattern',
         'v6c-mid345', 'v6c-mid0', 'v6c-mid15',
         'v6d-mild', 'v6d-mid', 'v6d-over',
         'v7-reef', 'v7-reef-pattern', 'v7-reef-over',
         'v7b-a', 'v7b-b', 'v7b-over',
         'v7c-family', 'v7c-bars', 'v7c-eyespot', 'v7c-edge']
OLD = S + '/hue-dials/map2/png/'


def main():
    base = json.load(open(SOURCE, encoding='utf-8'))
    for look in LOOKS:
        d = S + '/full-colour/' + look
        os.makedirs(d + '/png', exist_ok=True)
        items = []
        for it in base['items']:
            assert it['png_file'].startswith(OLD), it['png_file']
            assert os.path.exists(it['genome_file']), it['genome_file']
            items.append(dict(it, png_file=d + '/png/' + it['png_file'][len(OLD):]))
        with open(d + '/list.json', 'w', encoding='utf-8') as f:
            json.dump(dict(base, items=items), f, indent=1)
        print(look, len(items))


if __name__ == '__main__':
    main()
