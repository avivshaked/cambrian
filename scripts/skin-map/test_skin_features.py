"""Synthetic test for skin_features.py: python -I test_skin_features.py

Builds a tiny fake run under scratch/skin-map/fixture/ in the formats the farm writes (config.json, genomes.jsonl.gz,
lineage.jsonl, snapshots/<9 digits>.jsonl.gz with slim rows), runs the tool end to end, and checks the SpeciesDistance
port against values worked out by hand from SpeciesDistance.cs.
"""
import copy
import filecmp
import gzip
import json
import math
import os
import random
import shutil
import subprocess
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import skin_features as sf  # noqa: E402

FIXTURE = os.path.join(os.path.dirname(os.path.dirname(HERE)), 'scratch', 'skin-map', 'fixture')   # gitignored, beside the data
RUN = os.path.join(FIXTURE, 'run')
RUN2 = os.path.join(FIXTURE, 'run2')


# ------------------------------------------------------------------ fake genomes


def mk_edge(child, pa=(1, 0, 0), ca=(-1, 0, 0), scale=(1, 1, 1), reflect=(False, False, False), terminal=False,
            orient=(0, 0, 0, 1)):
    return {'id': '%016x' % (child + 1), 'child': child, 'terminalOnly': terminal,
            'parentAnchor': dict(zip('xyz', pa)), 'childAnchor': dict(zip('xyz', ca)),
            'scale': dict(zip('xyz', scale)),
            'orientation': dict(zip('xyzw', orient)), 'reflect': dict(zip('xyz', reflect))}


def mk_node(cell='structural', shape='box', joint='Fixed', dims=(0.1, 0.1, 0.1), edges=(), rec=1):
    return {'cell': cell, 'shape': shape, 'joint': joint, 'power': 1.0,
            'drive': {'threshold': 0, 'reaction': 1, 'hold': 0, 'release': 0}, 'lift': 0.0, 'recursiveLimit': rec,
            'growth': 'Determinate', 'maxModules': 1, 'attack': 0.0, 'intake': 0.0, 'protection': 0.0,
            'toughness': 0.0, 'buoyancyOffset': 0.0, 'dimensions': dict(zip('xyz', dims)),
            'jointLimits': [{'min': -1.0, 'max': 1.0}] if joint != 'Fixed' else [],
            'edges': list(edges), 'neurons': []}


def mk_genome(nodes, inv=0.3, margin=2.0, adult=1.0, weights=(0.5, -0.5, 1.0)):
    return {'format': 12, 'root': 0, 'nextEdgeId': 99, 'adultScale': adult, 'lifespan': 1.0,
            'reproduction': {'brood': 2, 'investment': inv, 'margin': margin, 'mode': 'Lump', 'gestation': 0.5},
            'nodes': nodes, 'globalBrain': [],
            'brain': {'outputs': [{'id': '%016x' % 1, 'bias': 0.0}], 'hidden': [{'id': '%016x' % 2, 'bias': 0.1}],
                      'links': [{'from': '%016x' % 3, 'to': '%016x' % 2, 'w': w} for w in weights]}}


def founder(kind):
    if kind == 0:  # a leaf with a mirrored pair of fins
        return mk_genome([
            mk_node('photosynthetic', 'box', dims=(0.2, 0.05, 0.3),
                    edges=[mk_edge(1, reflect=(True, False, False))]),
            mk_node('link', 'capsule', 'Hinge', dims=(0.04, 0.2, 0.04))], inv=0.2, margin=1.0)
    if kind == 1:  # a chain with a mouth
        return mk_genome([
            mk_node('structural', 'capsule', dims=(0.08, 0.2, 0.08), edges=[mk_edge(1)]),
            mk_node('link', 'capsule', 'Hinge', dims=(0.06, 0.15, 0.06), edges=[mk_edge(2)]),
            mk_node('consumer', 'sphere', dims=(0.07, 0.07, 0.07))], inv=0.4, margin=3.0)
    return mk_genome([
        mk_node('absorptive', 'sphere', dims=(0.12, 0.12, 0.12), edges=[mk_edge(1, scale=(0.8, 0.8, 0.8))], rec=1),
        mk_node('buoyancy', 'sphere', dims=(0.1, 0.1, 0.1))], inv=0.3, margin=0.0, weights=(2.0, 1.5))


def mutate(g, rng, cells=('structural', 'link', 'photosynthetic')):
    g = copy.deepcopy(g)
    for node in g['nodes']:
        if rng.random() < 0.5:
            for ax in 'xyz':
                node['dimensions'][ax] *= 1.0 + rng.gauss(0, 0.05)
    if rng.random() < 0.2:
        g['reproduction']['investment'] = max(0.05, g['reproduction']['investment'] + rng.gauss(0, 0.03))
    if rng.random() < 0.15:
        g['reproduction']['margin'] = max(0.0, g['reproduction']['margin'] + rng.gauss(0, 0.5))
    if rng.random() < 0.8:
        link = rng.choice(g['brain']['links'])
        link['w'] += rng.gauss(0, 0.2)
    if rng.random() < 0.1:
        g['nodes'][-1]['cell'] = rng.choice(list(cells))
    return g


REGISTRY = ['structural', 'link', 'neural', 'photosynthetic', 'absorptive', 'consumer', 'buoyancy', 'anchoring']
REGISTRY2 = ['structural', 'link', 'neural', 'photosynthetic', 'absorptive', 'consumer', 'buoyancy', 'fat']


def write_run(run_dir, seed, registry, cells, force_cell=None):
    """A fake run: three founders, three generations of two children each, the last generation alive at 24000 s."""
    os.makedirs(os.path.join(run_dir, 'snapshots'))
    rng = random.Random(seed)
    genomes = {}
    lineage = []
    next_id = 1
    gen = []
    for k in range(3):
        g = founder(k)
        genomes[next_id] = g
        lineage.append({'e': 'b', 't': 0, 'id': next_id, 'p': -1, 'k': 'founder', 'g': 0, 's': k})
        gen.append(next_id)
        next_id += 1
    for depth in range(1, 4):
        nxt = []
        for pid in gen:
            for _ in range(2):
                genomes[next_id] = mutate(genomes[pid], rng, cells)
                lineage.append({'e': 'b', 't': depth * 1000, 'id': next_id, 'p': pid, 'k': 'birth', 'g': depth, 's': 0})
                nxt.append(next_id)
                next_id += 1
        gen = nxt
    alive = gen
    if force_cell:  # a body with a part of a type the other registry has no column for
        genomes[alive[1]]['nodes'][-1]['cell'] = force_cell
    lineage.append({'e': 'd', 't': 5000, 'id': 1, 'c': 'aged'})
    with gzip.open(os.path.join(run_dir, 'genomes.jsonl.gz'), 'wt', encoding='utf-8') as f:
        for i in sorted(genomes):
            row = {'id': i}
            row.update(genomes[i])
            f.write(json.dumps(row, separators=(',', ':')) + '\n')
    with open(os.path.join(run_dir, 'lineage.jsonl'), 'w', encoding='utf-8') as f:
        for r in lineage:
            f.write(json.dumps(r, separators=(',', ':')) + '\n')
    with gzip.open(os.path.join(run_dir, 'snapshots', '%09d.jsonl.gz' % 24000), 'wt', encoding='utf-8') as f:
        for i in alive:
            f.write(json.dumps({'id': i, 'bf': 1.0}, separators=(',', ':')) + '\n')
    cfg = {'format': 2, 'configHash': 'x',
           'species': {'speciesCellTypeWeight': 1.0, 'speciesTopologyWeight': 1.0, 'speciesParameterWeight': 1.0,
                       'speciesBrainWeight': 0.0, 'speciesDriftThreshold': 0.0},
           'development': {'maxParts': 16, 'maxDepth': 8, 'minPartVolume': 1e-4, 'maxPartVolume': 1e6,
                           'minPartHalfExtent': 0.01, 'maxBodyReachMetres': 0.0, 'floorsWeighRigidGroups': False,
                           'scaleLengthensOnce': False, 'reflectMirroredAnchors': False},
           'cellTypes': [{'id': c} for c in registry]}
    with open(os.path.join(run_dir, 'config.json'), 'w', encoding='utf-8') as f:
        json.dump(cfg, f, indent=2)
    return genomes, alive


def build_fixture():
    if os.path.exists(FIXTURE):
        shutil.rmtree(FIXTURE)
    genomes, alive = write_run(RUN, 7, REGISTRY, ('structural', 'link', 'photosynthetic'))
    # A second world whose registry differs: it has a type ("fat") with no column and lacks "anchoring".
    write_run(RUN2, 11, REGISTRY2, ('structural', 'link', 'photosynthetic', 'fat', 'consumer'), force_cell='fat')
    return genomes, alive


# ------------------------------------------------------------------ tests


class SpeciesDistancePort(unittest.TestCase):
    W = (1.0, 1.0, 1.0, 0.0)

    def d(self, a, b, w=None):
        return sf.species_distance(sf.compact_for_distance(a), sf.compact_for_distance(b), w or self.W)

    def base(self):
        return mk_genome([mk_node('structural', 'box', dims=(0.1, 0.1, 0.1), edges=[mk_edge(1)]),
                          mk_node('link', 'capsule', 'Hinge', dims=(0.1, 0.2, 0.1))], inv=0.2)

    def test_identical_is_zero(self):
        g = self.base()
        self.assertEqual(self.d(g, copy.deepcopy(g)), 0.0)

    def test_one_cell_type_change_is_exactly_one(self):
        # SpeciesDistance.cs CellTypeUnits: node 1 link -> consumer is one type lost and one gained; half of 2 is 1.0
        a, b = self.base(), self.base()
        b['nodes'][1]['cell'] = 'consumer'
        self.assertAlmostEqual(self.d(a, b), 1.0, places=9)
        self.assertAlmostEqual(self.d(a, b, (3.0, 0.0, 0.0, 0.0)), 3.0, places=9)

    def test_topology_and_parameters_by_hand(self):
        a, b = self.base(), self.base()
        b['nodes'][0]['dimensions']['x'] = 0.3            # RelativeDiff(0.1, 0.3) = 0.2 / 0.4 = 0.5
        b['reproduction']['investment'] = 0.6             # RelativeDiff(0.2, 0.6) = 0.4 / 0.8 = 0.5
        b['nodes'][1]['shape'] = 'sphere'                 # topology +1
        b['nodes'][0]['edges'][0]['parentAnchor'] = {'x': 0, 'y': 1, 'z': 0}  # BoundedDiff = (1 + 1 + 0) / 6 = 1/3
        params = 0.5 + 0.5 + 1.0 / 3.0
        self.assertAlmostEqual(self.d(a, b, (1.0, 0.0, 0.0, 0.0)), 0.0, places=9)
        self.assertAlmostEqual(self.d(a, b, (0.0, 2.0, 0.0, 0.0)), 2.0, places=9)
        self.assertAlmostEqual(self.d(a, b, (0.0, 0.0, 0.5, 0.0)), 0.5 * params, places=9)
        self.assertAlmostEqual(self.d(a, b), 0.0 + 1.0 + params, places=9)
        self.assertAlmostEqual(self.d(a, b), self.d(b, a), places=12)

    def test_extra_node_and_edges(self):
        a, b = self.base(), self.base()
        b['nodes'].append(mk_node('photosynthetic', 'sphere'))
        b['nodes'][1]['edges'].append(mk_edge(2, reflect=(True, False, False)))
        # cell: multiset {structural, link} against {structural, link, photosynthetic}: sum|diff| = 1, half = 0.5
        # topology: node count 1, node 1 edge count 1 = 2.0; parameters: matched nodes are equal = 0
        self.assertAlmostEqual(self.d(a, b), 0.5 + 2.0, places=9)

    def test_growth_reserve_and_brain_terms(self):
        a, b = self.base(), self.base()
        a['growthReserve'] = 3.0
        b['growthReserve'] = 30.0
        self.assertAlmostEqual(self.d(a, b), math.log(10.0) / math.log(300.0), places=9)
        c, e = self.base(), self.base()
        c['nodes'][1]['neurons'] = [{'op': 'Sine', 'frequency': 1.0, 'phase': 0.0, 'amplitude': 1.0, 'bias': 0.0,
                                     'inputs': [{'kind': 'Sensor', 'index': 0, 'channel': 'Depth', 'constant': 0.0,
                                                 'weight': 1.0}]}]
        e['nodes'][1]['neurons'] = copy.deepcopy(c['nodes'][1]['neurons'])
        e['nodes'][1]['neurons'][0]['frequency'] = 3.0        # RelativeDiff(1, 3) = 2 / 4 = 0.5
        e['nodes'][1]['neurons'][0]['phase'] = math.pi / 2    # AngularDiff = (pi / 2) / pi = 0.5
        e['nodes'][1]['neurons'][0]['inputs'][0]['weight'] = 3.0  # 0.5
        self.assertAlmostEqual(self.d(c, e, (0.0, 0.0, 0.0, 2.0)), 2.0 * 1.5, places=6)
        e['nodes'][1]['neurons'][0]['inputs'][0]['channel'] = 'Light'  # a rewired sensor is a topology unit
        self.assertAlmostEqual(self.d(c, e, (0.0, 1.0, 0.0, 0.0)), 1.0, places=9)


class Development(unittest.TestCase):
    def test_mirrored_pair_counts(self):
        g = mk_genome([mk_node('structural', 'box', dims=(0.1, 0.1, 0.1),
                               edges=[mk_edge(1, reflect=(True, False, False))]),
                       mk_node('link', 'sphere', 'Hinge', dims=(0.05, 0.05, 0.05))])
        parts = sf.develop(g, sf.Limits())
        self.assertEqual(len(parts), 3)
        self.assertEqual(sum(1 for p in parts if p.mirrored), 1)
        self.assertAlmostEqual(parts[0].volume, 8 * 0.1 ** 3, places=12)
        # the unreflected child sits on the +x face; the reflected twin on the -x face
        xs = sorted(round(float(p.pos[0]), 6) for p in parts[1:])
        self.assertAlmostEqual(xs[0], -xs[1], places=6)

    def test_recursion_scale_and_volume_floor(self):
        g = mk_genome([mk_node('structural', 'box', dims=(0.1, 0.1, 0.1), edges=[mk_edge(0, scale=(0.5, 0.5, 0.5))],
                               rec=6)])
        parts = sf.develop(g, sf.Limits())
        # half-extents 0.1, 0.05, 0.025, 0.0125, then 0.01 (thickened): volumes 8e-3, 1e-3, 1.25e-4, 1.5e-5 -> pruned
        self.assertEqual(len(parts), 3)

    def test_superellipsoid_volume_matches_box_and_ellipsoid_limits(self):
        # profile = section = 1 is the ellipsoid with a taper of 1 (k = 0): volume 4/3 pi a b c
        s = sf.Superellipsoid(1.0, 1.0, 1.0)
        self.assertAlmostEqual(s.volume_factor, 4.0 / 3.0 * math.pi, places=9)

    def test_features_row_shape(self):
        g = founder(1)
        names = [t[0] for t in sf.feature_table(sf.DEFAULT_CELL_IDS)]
        row = sf.features_of(g, sf.Limits(), sf.DEFAULT_CELL_IDS)
        self.assertEqual(len(row), len(names))
        shares = [v for n, v in zip(names, row) if n.startswith('share_')]
        self.assertAlmostEqual(sum(shares), 1.0, places=9)


class Folding(unittest.TestCase):
    def test_fold_gain_maps_p10_median_p90(self):
        s = sf.np.arange(101, dtype=float)  # median 50, p10 10, p90 90
        g, st = sf.fold_gain(s)
        self.assertEqual((st['median'], st['p10'], st['p90']), (50.0, 10.0, 90.0))
        self.assertAlmostEqual(sf.logistic(g * (90.0 - 50.0)), 0.9, places=12)
        self.assertAlmostEqual(sf.logistic(g * (10.0 - 50.0)), 0.1, places=12)
        self.assertAlmostEqual(sf.logistic(g * (50.0 - 50.0)), 0.5, places=12)

    def test_degenerate_spread_is_kept_and_reported(self):
        g, st = sf.fold_gain(sf.np.zeros(50))
        self.assertIsNone(g)
        resolved = [{'name': 'p', 'bias': 0.25, 'inputs': [{'feature': 'a', 'weight': 0.8, 'used': True}]}]
        sf.fold_parameters(resolved, sf.np.zeros((50, 1)), {'a': 0})
        self.assertFalse(resolved[0]['fold']['applied'])
        self.assertEqual(resolved[0]['inputs'][0]['weight'], 0.8)
        self.assertEqual(resolved[0]['bias'], 0.25)
        self.assertIn('degenerate', resolved[0]['fold']['reason'])

    def test_fold_applies_to_inputs_and_bias(self):
        z = sf.np.linspace(-2, 2, 101).reshape(-1, 1)
        resolved = [{'name': 'p', 'bias': 0.0, 'inputs': [{'feature': 'a', 'weight': 0.5, 'used': True}]}]
        sf.fold_parameters(resolved, z, {'a': 0})
        S = sf.skin_vectors(z, {'a': 0}, resolved, 3.0)[:, 0]
        self.assertAlmostEqual(float(S[50]), 0.5, places=9)
        self.assertAlmostEqual(float(sf.np.percentile(S, 90)), 0.9, places=9)
        self.assertAlmostEqual(float(sf.np.percentile(S, 10)), 0.1, places=9)

    def test_parse_ref(self):
        self.assertEqual(sf.parse_ref('runs/x/y@24000'), ('runs/x/y', 24000.0))
        with self.assertRaises(sf.ToolError):
            sf.parse_ref('runs/x/y')


class Statistics(unittest.TestCase):
    def test_spearman_with_ties(self):
        self.assertAlmostEqual(sf.spearman([1, 2, 3, 4, 5], [10, 20, 30, 40, 50]), 1.0, places=12)
        self.assertAlmostEqual(sf.spearman([1, 2, 3, 4, 5], [5, 4, 3, 2, 1]), -1.0, places=12)
        # ranks of x: 1.5 1.5 3 4; of y: 1 2 3 4 -> hand value 0.948683...
        self.assertAlmostEqual(sf.spearman([1, 1, 2, 3], [1, 2, 3, 4]), 0.9486832980505138, places=9)


class EndToEnd(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.genomes, cls.alive = build_fixture()

    def run_tool(self, name):
        out = os.path.join(FIXTURE, name)
        args = sf.build_parser().parse_args([RUN, '--at', '23000', '--out', out, '--bootstrap', '20',
                                             '--check-pairs', '300', '--strangers', '300', '--max-cousins', '300'])
        old = sys.stdout
        sys.stdout = open(os.devnull, 'w')
        try:
            res = sf.run_tool(args)
        finally:
            sys.stdout.close()
            sys.stdout = old
        return out, res

    def test_end_to_end(self):
        out, res = self.run_tool('out1')
        for name in ('features.csv', 'parent_child_check.md', 'projection.json', 'weights.json', 'weights_table.md',
                     'pairs.csv', 'summary.md', 'skin_vectors.csv', 'pairs_scatter.png'):
            self.assertTrue(os.path.exists(os.path.join(out, name)), name)
        self.assertEqual(res['n_ids'], 45)  # 24 alive (generation 3) plus three generations of ancestors
        self.assertFalse(math.isnan(res['rho']))
        with open(os.path.join(out, 'weights.json'), encoding='utf-8') as f:
            w = json.load(f)
        self.assertEqual(len(w['parameters']), 10)
        S = res['S']
        self.assertEqual(S.shape, (45, 10))
        self.assertTrue(((S >= 0) & (S <= 1)).all())
        with open(os.path.join(out, 'projection.json'), encoding='utf-8') as f:
            p = json.load(f)
        self.assertEqual(p['snapshot_seconds'], 24000)  # nearest to 23000
        self.assertEqual(p['n_reference'], 24)
        self.assertIn('investment', p['features_passing'])
        self.assertIn('share_neural', p['features_constant'])
        with open(os.path.join(out, 'pairs.csv'), encoding='utf-8') as f:
            classes = {line.split(',')[0] for line in f.read().splitlines()[1:]}
        self.assertTrue({'parent_child', 'sibling', 'cousin', 'stranger'} <= classes, classes)
        with open(os.path.join(out, 'summary.md'), encoding='utf-8') as f:
            text = f.read()
        self.assertIn('Spearman rho', text)
        self.assertIn('Corner share', text)
        # three founders, so three clades and strangers are cross-clade
        self.assertIn('3 clades', text)

    def pooled(self, name, refs=None):
        out = os.path.join(FIXTURE, name)
        refs = refs if refs is not None else ['%s@24000' % RUN, '%s@23000' % RUN2, os.path.join(FIXTURE, 'nowhere') + '@24000']
        argv = [RUN, '--at', '24000', '--out', out, '--bootstrap', '20', '--check-pairs', '300', '--strangers', '300',
                '--max-cousins', '300', '--no-plot']
        for r in refs:
            argv += ['--ref', r]
        old = sys.stdout
        sys.stdout = open(os.devnull, 'w')
        try:
            res = sf.run_tool(sf.build_parser().parse_args(argv))
        finally:
            sys.stdout.close()
            sys.stdout = old
        return out, res

    def test_pooled_reference_and_recentring(self):
        out, res = self.pooled('pooled1')
        names = [t[0] for t in sf.feature_table(REGISTRY)]
        with open(os.path.join(out, 'projection.json'), encoding='utf-8') as f:
            p = json.load(f)
        self.assertTrue(p['pooled'])
        self.assertEqual(p['cell_type_columns'], REGISTRY)  # one fixed set, the primary's registry
        self.assertEqual(p['features_all'], names)
        runs = p['reference_runs']
        self.assertEqual(len(runs), 3)
        # the expected non-producers, worked out independently from the genomes
        for run_dir, info in ((RUN, runs[0]), (RUN2, runs[1])):
            with gzip.open(os.path.join(run_dir, 'genomes.jsonl.gz'), 'rt', encoding='utf-8') as f:
                gs = [json.loads(line) for line in f]
            with gzip.open(os.path.join(run_dir, 'snapshots', '000024000.jsonl.gz'), 'rt', encoding='utf-8') as f:
                alive = {json.loads(line)['id'] for line in f}
            lim = sf.Limits(sf.load_config(run_dir))
            n_np = n_p = n_out = 0
            for g in gs:
                if g['id'] not in alive:
                    continue
                parts = sf.develop(g, lim)
                if any(q.cell not in REGISTRY for q in parts):
                    n_out += 1
                row = sf.features_of(g, lim, REGISTRY, parts=parts)
                if sf.is_producer_row(row, names):
                    n_p += 1
                else:
                    n_np += 1
            self.assertEqual(info['alive'], len(alive))
            self.assertEqual(info['reference_bodies'], n_np)
            self.assertEqual(info['producers_left_out'], n_p)
            self.assertEqual(info['bodies_with_parts_outside_the_columns'], n_out if info is runs[1] else 0)
            self.assertIsNone(info['error'])
        self.assertGreater(runs[0]['producers_left_out'], 0)       # the leaf lineage is a producer
        self.assertGreater(runs[0]['reference_bodies'], 0)
        self.assertEqual(runs[0]['source'], "the primary run's own rows")
        self.assertEqual(runs[1]['snapshot_seconds'], 24000)       # 23000 asked, nearest snapshot
        self.assertEqual(runs[1]['registry_types_with_no_column'], ['fat'])
        self.assertEqual(runs[1]['columns_with_no_cell_type_in_registry'], ['anchoring'])
        self.assertGreater(runs[1]['bodies_with_parts_outside_the_columns'], 0)
        self.assertIsNotNone(runs[2]['error'])                     # a failed run is reported, not hidden
        self.assertEqual(runs[2]['reference_bodies'], 0)
        self.assertEqual(p['n_reference'], runs[0]['reference_bodies'] + runs[1]['reference_bodies'])
        self.assertEqual(res['n_reference'], p['n_reference'])
        self.assertIn('non-producers', p['reference_population'])
        # re-centring: every folded parameter puts the reference's median body at 0.5, and p10 below p90
        with open(os.path.join(out, 'weights.json'), encoding='utf-8') as f:
            w = json.load(f)
        self.assertTrue(w['folded'])
        Sref = res['S_ref']
        self.assertEqual(Sref.shape[0], p['n_reference'])
        folded = 0
        for k, rec in enumerate(w['parameters']):
            fo = rec['fold']
            if fo['applied']:
                folded += 1
                self.assertAlmostEqual(float(sf.np.median(Sref[:, k])), 0.5, delta=0.06, msg=rec['name'])
                self.assertLess(float(sf.np.percentile(Sref[:, k], 10)), float(sf.np.percentile(Sref[:, k], 90)))
                for i in [i for i in rec['inputs'] if i['used']]:
                    self.assertAlmostEqual(i['weight'], i['weight_before_fold'] * fo['gain'], places=9)
                self.assertAlmostEqual(rec['bias'], -fo['gain'] * fo['median'], places=9)
            else:
                self.assertTrue(fo['reason'])
        self.assertGreater(folded, 3)
        with open(os.path.join(out, 'summary.md'), encoding='utf-8') as f:
            text = f.read()
        for needle in ('non-producers', 'FAILED', 'both bodies are non-producers', 'p10 / p50 / p90'):
            self.assertIn(needle, text)
        self.assertTrue(os.path.exists(os.path.join(out, 'pairs_nonproducer_sample.csv')))

    def test_no_reference_run_readable_is_an_error(self):
        with self.assertRaises(sf.ToolError):
            self.pooled('pooled_bad', refs=[os.path.join(FIXTURE, 'nowhere') + '@24000'])

    def test_deterministic(self):
        a, _ = self.run_tool('det1')
        b, _ = self.run_tool('det2')
        for name in ('features.csv', 'pairs.csv', 'projection.json', 'weights.json', 'summary.md'):
            with open(os.path.join(a, name), encoding='utf-8') as fa, open(os.path.join(b, name), encoding='utf-8') as fb:
                self.assertEqual(fa.read(), fb.read(), name)

    def test_resumed_segment_with_prior(self):
        main = os.path.join(FIXTURE, 'seg2')
        prior = os.path.join(FIXTURE, 'seg1')
        for d in (main, prior):
            os.makedirs(os.path.join(d, 'snapshots'))
        shutil.copy(os.path.join(RUN, 'config.json'), os.path.join(main, 'config.json'))
        shutil.copy(os.path.join(RUN, 'config.json'), os.path.join(prior, 'config.json'))
        shutil.copy(os.path.join(RUN, 'snapshots', '000024000.jsonl.gz'), os.path.join(main, 'snapshots'))
        with gzip.open(os.path.join(RUN, 'genomes.jsonl.gz'), 'rt', encoding='utf-8') as f:
            rows = f.read().splitlines()
        with open(os.path.join(RUN, 'lineage.jsonl'), encoding='utf-8') as f:
            lin = f.read().splitlines()
        early = {l for l in range(1, 10)}
        for d, keep in ((prior, lambda i: i in early), (main, lambda i: i not in early)):
            with gzip.open(os.path.join(d, 'genomes.jsonl.gz'), 'wt', encoding='utf-8') as f:
                for r in rows:
                    if keep(json.loads(r)['id']):
                        f.write(r + '\n')
            with open(os.path.join(d, 'lineage.jsonl'), 'w', encoding='utf-8') as f:
                for r in lin:
                    j = json.loads(r)
                    if j['e'] == 'b' and keep(j['id']):
                        f.write(r + '\n')
        common = ['--at', '24000', '--bootstrap', '0', '--check-pairs', '200', '--no-plot']
        old = sys.stdout
        sys.stdout = open(os.devnull, 'w')
        try:
            alone = sf.run_tool(sf.build_parser().parse_args([main, '--out', os.path.join(FIXTURE, 'o_alone')] + common))
            both = sf.run_tool(sf.build_parser().parse_args(
                [main, '--prior', prior, '--out', os.path.join(FIXTURE, 'o_both')] + common))
        finally:
            sys.stdout.close()
            sys.stdout = old
        self.assertEqual(both['n_ids'], 45)
        self.assertLess(alone['n_ids'], 45)

    def test_cli(self):
        out = os.path.join(FIXTURE, 'cli')
        r = subprocess.run([sys.executable, '-I', os.path.join(HERE, 'skin_features.py'), RUN, '--at', '24000',
                            '--out', out, '--no-plot', '--bootstrap', '0', '--check-pairs', '200'],
                           capture_output=True, text=True)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertTrue(os.path.exists(os.path.join(out, 'summary.md')))
        self.assertFalse(os.path.exists(os.path.join(out, 'pairs_scatter.png')))


if __name__ == '__main__':
    unittest.main(verbosity=2)
