"""The leaf tank's recipes (logbook/specs/nursery-leaf-tank-plan.md, steps 5 and 6), written from one set of world
overrides so that the drag check and the leaf arms stand in the same water.

    python scripts/reads/leaf-recipes.py <repository root> drag --pool rounds/nursery/pools/a326093a77658bdc.tsv

writes the five drag-check benches into rounds/nursery/.

    python scripts/reads/leaf-recipes.py <repository root> smoke

writes step 7's smoke: each arm for one generation of a small search, and the direction arm again with a 3 m sense.

    python scripts/reads/leaf-recipes.py <repository root> tops --form Strength --pool rounds/nursery/pools/<hash>.tsv

writes the bench of an arm's saved tops, from the pool tops-pool.py made of the finished arm.

    python scripts/reads/leaf-recipes.py <repository root> sparse

writes the second pair of arms (D173): 31 leaves, a 6 m reach, D172's stop and exam. `sparse3` writes D174's check,
the direction arm at 3 m.

    python scripts/reads/leaf-recipes.py <repository root> bodies --config-hash <hash>

writes D178's first arm, bodies evolving with brains, on D177's config named by its hash. `return`, with the same
hash, writes its second arm, scored on the energy return; `repeat --seed 0x2` writes that arm on another recipe seed.
`patches`, with the same hash, writes D182's patchy tank as D184 laid it out, its repeat on seed 0x2, and the two pace
benches; `founders` writes each arm's exam swum by the founders, the baseline of the patch measures.
"""
import argparse
import json
import os

CONFIG = 'rounds/nursery/configs/3fd98985cdf68008.json'
CONFIG_HASH = '3fd98985cdf68008'

# r58-s3j's world made brain2's tank (the coordinator, 2026-10-12): a 40 by 40 m tank, the floor flat at the world's 25 m
# with no tilt, no shore and no reef, none of the config's seeded matter and so no matter islands, and no founding trickle (the nursery refuses
# one). The newborn mass floor and everything else are round 58's.
# The bred bodies start in the leaves' band (the coordinator, 2026-10-13): the nursery puts a body at the bed plus aboveBed
# times a draw from 0.5 to 1.5, so 11.5 above a floor at 25 m spans 19.25 to 7.75 m deep, the band's 19.2 to 6.9 m. The
# swimmer is under round 58's neutral volume, so it stays at the height it starts at.
ABOVE_BED = 11.5

WORLD = {
    'WorldAreaSquareMetres': 1600,
    'BedTiltMetres': 0,
    'BedShoreDepthMetres': 0,
    'BedShoreFadeMetres': 0,
    'ReefCover': 0,
    'InitialMatterPerCubicMetre': 0,
    'MatterBudgetUnits': 0,
    'MatterIslandWavelengthMetres': 0,
    'FoundingTricklePerSecond': 0,
    'FoundingTricklePoolCount': 0,
    'FoundingTricklePoolShare': 0,
    'FoundingTricklePoolHash': '',
    'UnbittenDeathsLeaveNoCorpse': True,
}


def recipe(name, description, overrides, pool, seconds, purse, leaves, search, reward, exam):
    return {
        'format': 1,
        'name': name,
        'description': description,
        'seed': '0x1',
        'world': {'config': CONFIG, 'configHash': CONFIG_HASH, 'overrides': dict(sorted(overrides.items()))},
        'bodies': {'source': 'pool', 'founders': 0, 'trunk': None, 'pool': pool, 'keep': None},
        'setup': {
            'seconds': seconds, 'dt': 0.01, 'tankSize': 8, 'floorClosed': True, 'birthsHeld': True, 'snow': None,
            'carrion': {'count': 0, 'joules': 40}, 'start': {'aboveBed': ABOVE_BED}, 'purse': {'rule': 'grown', 'seconds': purse},
        },
        **({'leaves': leaves} if leaves is not None else {}),
        'search': search,
        'reward': reward,
        'exam': exam,
    }


def write(wt, r):
    path = os.path.join(wt, 'rounds', 'nursery', r['name'] + '.json')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(r, f, indent=2)
        f.write('\n')
    print(path)


def drag(wt, pool):
    conditions = [('off', 'None', None, None), ('skin-0p05', 'Skin', 'SkinFrictionCoefficient', 0.05),
                  ('skin-0p5', 'Skin', 'SkinFrictionCoefficient', 0.5), ('spin-0p05', 'Spin', 'SpinDragCoefficient', 0.05),
                  ('spin-0p5', 'Spin', 'SpinDragCoefficient', 0.5)]
    for tag, form, key, size in conditions:
        ov = dict(WORLD)
        ov['Fluid.TurningDrag'] = form
        if key:
            ov['Fluid.' + key] = size
        said = 'drag off' if key is None else '%s form, coefficient %g' % (form.lower(), size)
        write(wt, recipe(
            'leaf-drag-' + tag,
            'The leaf tank\'s drag check (D161; nursery-leaf-tank-plan.md, step 6), %s: the leaf world with no leaves, two '
            'hand-built swimmers and six of r58-s3j\'s single stomachs in one tank, every body under its own drawn brain and under a constant power '
            'of 0.5, for 300 s. Read under the constant power for each body\'s turn and path, and the swimmers\' speed after the first step.' % said,
            ov, pool, 300, None, None, None, {'class': 'Net', 'parameters': {}},
            {'episodes': 2, 'salt': '0x62656e6368', 'controls': [{'kind': 'own'}, {'kind': 'constant', 'power': 0.5}]}))


# The arms (the coordinator, 2026-10-13): the drag on turning in its spin form at 0.5, episodes of 1,800 s, and a purse of
# 3,830 s of standing watts, 1.5 episodes plus the work of a stroke at full power. The leaves are step 1's pool at the
# density r58-s3j's band held, 250 to the tank, in the band's spent matter.
ARM = {'Fluid.TurningDrag': 'Spin', 'Fluid.SpinDragCoefficient': 0.5}
SECONDS = 1800
PURSE = 3830
SWIMMER = 'rounds/nursery/pools/6a27d8acada28e03.tsv'
LEAVES = {'pool': 'rounds/nursery/pools/leaves-3f6c390cd561cb09.tsv.gz', 'count': 250,
          'spentUnitsPerCubicMetre': 0.001033375988005691}
# The score (the owner's ruling of 2026-10-13, D170): eaten, the energy a mouth took from the leaves bitten to death,
# with Net reported beside it. The smoke's recipes were written before the ruling and say so.
REWARD = {'class': 'WeightedSum', 'parameters': {'weights': {'eaten': 1}}}
SEARCH = {'population': 96, 'draws': 16, 'generations': 100, 'elites': 8, 'tournament': 3, 'perBody': True,
          'evolve': 'brain', 'evolveEnvelope': False, 'crossover': {'rate': 0.5, 'baseShare': 0.7, 'mutationByDistance': 0.785}}


def arm(form, smoke, rng=None):
    ov = dict(WORLD)
    ov.update(ARM)
    ov['LeafScent'] = form
    if rng is not None:
        ov['LeafScentRangeMetres'] = rng
    channels = ['LeafScent'] if form == 'Strength' else ['LeafScent', 'LeafScentDirection']
    search = dict(SEARCH)
    if smoke:
        search.update({'population': 16, 'draws': 2, 'generations': 1, 'elites': 2})
    name = 'leaf-' + form.lower() + ('-smoke' if smoke else '') + ('' if rng is None else '-r%g' % rng)
    said = ("The leaf tank (D166, D167), the %s form of the leaf smell%s: the hand-built swimmer, its brain bred in a 40 m "
            "tank of 250 of r58-s3j's living leaves at their own depths, the swimmers started in the leaves' band; scored on "
            "what its mouth kept (eaten), with Net beside it, until the owner rules the score." %
            (form.lower(), '' if rng is None else ', sensed to %g m' % rng))
    if smoke:
        said += " Step 7's smoke: one generation of a small search."
    exam = {'episodes': 2 if smoke else 32, 'salt': '0x62656e6368',
            'controls': [{'kind': 'own'}, {'kind': 'brainless'}, {'kind': 'knockout', 'channels': channels}]}
    return recipe(name, said, ov, SWIMMER, SECONDS, PURSE, LEAVES, search, REWARD, exam)


# The run (the owner's rulings of 2026-10-13, through the coordinator): a 20 m tank holding the band's density, 62 leaves,
# sensed to 3 m; the age hazard off in the tank (Core refuses the three ageing settings beside a hazard that is off);
# r58-s3j's whole-water spent matter at 44,000 s, 2,329.2 units over 549,727.5 m^3, so that the leaves do not starve; two
# arms of 30 generations, population 48, 8 draws, side by side.
RUN_WORLD = {'WorldAreaSquareMetres': 400, 'LeafScentRangeMetres': 3,
             'AgeHazardBaseSeconds': 0, 'AgeingPerBirth': 0, 'AgeingPerWound': 0, 'AgeRepairExponent': 0}
RUN_LEAVES = {'pool': LEAVES['pool'], 'count': 62, 'spentUnitsPerCubicMetre': 0.004237}
RUN_SEARCH = {'population': 48, 'draws': 8, 'generations': 30}


def run_arm(form):
    ov = dict(WORLD)
    ov.update(ARM)
    ov.update(RUN_WORLD)
    ov['LeafScent'] = form
    channels = ['LeafScent'] if form == 'Strength' else ['LeafScent', 'LeafScentDirection']
    search = dict(SEARCH)
    search.update(RUN_SEARCH)
    said = ("The leaf tank (D166, D167), the %s form of the leaf smell, sensed to 3 m: the hand-built swimmer, its brain "
            "bred in a 20 m tank of 62 of r58-s3j's living leaves at their own depths, in r58-s3j's whole-water spent "
            "matter, with the age hazard off in the tank; eight swimmers to a tank, started in the leaves' band. Scored on "
            "the energy its mouth took from the leaves it killed (eaten, D170), with Net, kills and meals per swimmer "
            "beside it." % form.lower())
    exam = {'episodes': 32, 'salt': '0x62656e6368',
            'controls': [{'kind': 'own'}, {'kind': 'brainless'}, {'kind': 'knockout', 'channels': channels}]}
    return recipe('leaf-' + form.lower(), said, ov, SWIMMER, SECONDS, PURSE, RUN_LEAVES, search, REWARD, exam)


# The second pair (D173, the owner's ruling of 2026-10-13): as the first pair, but 31 leaves to a tank, drawn from the
# same pool by the same partial shuffle and placed largest first, the smell's reach 6 m, D172's stop (eaten, window 5,
# gain 5%, over 10, floor 20) under a cap of 60 generations, and D172's exam of every generation's top and the last
# elites under the same three brains. The seed, the salts and the water's spent matter are the first pair's.
SPARSE_LEAVES = dict(RUN_LEAVES, count=31)
STOP = {'measure': 'eaten', 'window': 5, 'gain': 0.05, 'over': 10, 'floor': 20}


def sparse_arm(form):
    r = run_arm(form)
    r['name'] = 'leaf-' + form.lower() + '-sparse'
    r['description'] = (
        "The leaf tank's second pair (D173), the %s form of the leaf smell, sensed to 6 m: as leaf-%s (D169), with 31 "
        "of r58-s3j's living leaves to the 20 m tank in place of 62. The search stops when eaten, averaged over 5 "
        "generations, gains no more than 5%% in 10, never before generation 20 and at 60 at most (D172); the exam swims "
        "every generation's top and the last elites under its own brain, no brain and the smell knocked out." %
        (form.lower(), form.lower()))
    r['world']['overrides']['LeafScentRangeMetres'] = 6
    r['leaves'] = dict(SPARSE_LEAVES)
    r['search']['generations'] = 60
    r['search']['stop'] = dict(STOP)
    r['exam']['pool'] = 'tops'
    return r


def sparse3_arm():
    """D174's check (the owner's approval of 2026-10-14): leaf-direction-sparse with the reach at 3 m, nothing else changed, to
    separate the reach from the leaf count, which D173's pair changed together."""
    r = sparse_arm('Direction')
    r['name'] = 'leaf-direction-sparse3'
    r['description'] = (
        "D174's check: leaf-direction-sparse (D173) with the smell's reach at 3 m in place of 6 m and nothing else changed, "
        "31 of r58-s3j's living leaves to the 20 m tank, D172's stop and exam of the tops. It separates the reach from the "
        "leaf count, which D173's pair changed together.")
    r['world']['overrides']['LeafScentRangeMetres'] = 3
    return r


# The tops' bench (D172, the coordinator, 2026-10-13): an arm's saved top of every generation, written by tops-pool.py,
# swum as it is in the arm's own world, eight to a tank, under the arm's three brains, on 32 episodes of a salt that
# neither the search (0x67656E) nor the exam (0x62656e6368) drew: "hold".
BENCH_SALT = '0x686f6c64'


def tops_bench(form, pool):
    r = run_arm(form)
    r['name'] = 'leaf-' + form.lower() + '-tops'
    r['description'] = (
        "The bench of leaf-%s's saved tops (D172): the top genome of each of its 31 generations, every brain as the "
        "search left it, swum eight to a tank in the arm's own world, on 32 held-out episodes, under its own brain, no "
        "brain and the leaf smell knocked out. Analysis of a finished arm; nothing is searched." % form.lower())
    r['bodies']['pool'] = pool
    r['search'] = None
    r['exam']['salt'] = BENCH_SALT
    return r


# D178 (the owner's ruling of 2026-10-14): bodies evolve with brains, scored on net energy. leaf-direction-sparse's tank,
# seeded from its ten best tops (inocula/r59-direction-tops/, D174) with their own brains, in D177's prices. The search
# mutates the whole genome with the world's operators, the set of cell types kept (evolve kind); crossover needs one body
# for both parents and is off. The reward is NetEnergy with a dead body charged to the episode's end, and the stop reads
# it (the coordinator, 2026-10-14). The exam is D172's, and still reads eaten.
CONFIG_D177 = 'rounds/nursery/configs/%s.json'
BODIES_POOL = 'rounds/nursery/pools/410a70db44a4b20a.tsv'


def bodies_arm(config_hash):
    r = sparse_arm('Direction')
    r['name'] = 'leaf-direction-bodies'
    r['description'] = (
        "D178's first arm, bodies evolving with brains: leaf-direction-sparse's tank (D173), the direction smell to 6 m and 31 of "
        "r58-s3j's living leaves, at D177's prices (consumer upkeep 2 W/m3, attack and intake 0.033 W a unit). It starts from "
        "the ten direction tops of inocula/r59-direction-tops with their own brains. The search mutates the whole genome with "
        "the world's operators, keeping the set of cell types, and has no crossover. It is scored on net energy (eaten less "
        "upkeep, work and handling, a dead body charged its upkeep to the episode's end), and stops when that score, averaged "
        "over 5 generations, gains no more than 5% of its size in 10, never before generation 20 and at 60 at most. The exam "
        "swims every generation's top and the last elites under its own brain, no brain and the smell knocked out.")
    r['world']['config'] = CONFIG_D177 % config_hash
    r['world']['configHash'] = config_hash
    r['bodies']['pool'] = BODIES_POOL
    r['bodies']['brains'] = 'kept'
    r['search']['evolve'] = 'kind'
    r['search']['crossover'] = None
    r['search']['stop'] = dict(STOP, measure='reward')
    r['reward'] = {'class': 'NetEnergy', 'parameters': {'chargeDeathToEnd': True}}
    return r


def return_arm(config_hash):
    """D178's second arm, written before the owner's ruling on it (the coordinator, 2026-10-14): the first arm with the score
    EnergyReturn, eaten over upkeep, work and handling, in place of NetEnergy, which the first arm showed is raised by
    idling. The stop reads eaten, so that a body eating a crumb at almost no cost cannot end the search."""
    r = bodies_arm(config_hash)
    r['name'] = 'leaf-direction-return'
    r['description'] = (
        "D178's second arm: leaf-direction-bodies with the score EnergyReturn (eaten over upkeep, work and handling, a dead "
        "body charged its upkeep to the episode's end) in place of net energy, which the first arm showed is raised by "
        "idling. The tank, the prices, the founders and their brains, the body search and the exam are the first arm's. "
        "The stop reads eaten, averaged over 5 generations: no more than 5% gained in 10, never before generation 20 and "
        "at 60 at most.")
    r['reward'] = {'class': 'EnergyReturn', 'parameters': {'chargeDeathToEnd': True}}
    r['search']['stop'] = dict(STOP)
    return r


def return_repeat(config_hash, seed):
    """leaf-direction-return on another recipe seed and nothing else changed: the repeat the coordinator asked for on
    2026-10-14 (beside D180) before a nursery result that a decision rests on is acted on."""
    r = return_arm(config_hash)
    r['name'] = 'leaf-direction-return-s%d' % int(seed, 16)
    r['description'] += " A repeat on recipe seed %s in place of 0x1, nothing else changed." % seed
    r['seed'] = seed
    return r


# D182 and D184 (the owner's rulings of 2026-10-15): the patchy tank. leaf-direction-return's recipe in a tank of 3,400 m²
# (radius 32.9 m), its 31 leaves in 3 equal patches of 6 m radius on a ring of 21 m, the ring turned each episode; the
# bred bodies start anywhere in the tank, as before.
PATCH_AREA = 3400
PATCHES = {'count': 3, 'radius': 6, 'ring': 21}


def patches_arm(config_hash, seed='0x1'):
    r = return_arm(config_hash)
    r['name'] = 'leaf-direction-patches' if seed == '0x1' else 'leaf-direction-patches-s%d' % int(seed, 16)
    r['description'] = (
        "D182's patchy tank (laid out as D184 approved): leaf-direction-return in a tank of 3,400 m2 in place of 400 m2, its "
        "31 leaves in 3 equal patches of 6 m radius whose centres lie 120 degrees apart on a ring of 21 m about the axis, the "
        "ring turned by an angle each episode draws, so 24 m of bare water lies between patches and 6 m between each patch "
        "and the glass. The bred bodies start anywhere in the tank. The score (EnergyReturn), the prices, the founders and "
        "their brains, the body search, the stop on eaten and the exam are leaf-direction-return's.")
    if seed != '0x1':
        r['description'] += " A repeat on recipe seed %s in place of 0x1, nothing else changed." % seed
    r['seed'] = seed
    r['world']['overrides']['WorldAreaSquareMetres'] = PATCH_AREA
    r['leaves']['patches'] = dict(PATCHES)
    return r


def pace_bench(config_hash, patched):
    """The pace bench D184 allowed: the arm's founders swum as they are, eight to a tank, for two episodes under their own
    brain, in the patchy tank, and its pair in leaf-direction-return's 400 m2 tank, so that the ratio of the two scales a
    generation timed in that arm. Nothing is searched; it times the tank, not a result."""
    r = patches_arm(config_hash) if patched else return_arm(config_hash)
    r['name'] = 'leaf-direction-%s-pace' % ('patches' if patched else 'return')
    r['description'] = (
        "Pace bench (D184): the founders of %s, eight to a tank, two episodes under their own brain, no search. It times the "
        "tank's episode and decides nothing." % ('leaf-direction-patches' if patched else 'leaf-direction-return'))
    del r['bodies']['brains']  # a bench swims the pool's genomes with their own brains; "kept" acts on a search's start
    r['search'] = None
    r['exam'] = {'episodes': 2, 'salt': BENCH_SALT, 'controls': [{'kind': 'own'}]}
    return r


def patches_founders(config_hash, seed='0x1'):
    """The founders' baseline for D182's patch measures (the coordinator, 2026-10-15): the arm's exam, on its own seed, salt and 32
    draws under its three brains, swum by the ten founders as they are, with no search. Its tanks are the arm's exam tanks, so the
    founders' patch-keeping is measured where the bred genomes' is; the brainless control is the chance baseline."""
    r = patches_arm(config_hash, seed)
    r['name'] += '-founders'
    r['description'] = (
        "The founders' baseline for D182's patch measures: %s's exam (its seed, salt, 32 draws and three brains) swum by the ten "
        "founders of inocula/r59-direction-tops as they are, eight to a tank, with no search." % r['name'][:-len('-founders')])
    del r['bodies']['brains']  # a bench swims the pool's genomes with their own brains
    r['search'] = None
    del r['exam']['pool']
    return r


def r59_d2(config_hash, world, pool):
    """Round 59's clause D2 (logbook/0140; the coordinator, 2026-10-15): a world's planted-descended grazers alive at 48,000 s, as
    r59-d2-pool.py wrote them, swum in D173's tank (31 leaves, the direction smell to 6 m) at D177's prices, round 59's own, under
    their own brain, no brain and the smell knocked out, on 32 held-out episodes: leaf-direction-bodies' world and exam, with no
    search, so its draws are the ones the ten tops' eating in origins.tsv came from."""
    r = bodies_arm(config_hash)
    r['name'] = 'r59-d2-' + world
    r['description'] = (
        "Round 59's clause D2: the planted-descended grazers alive at 48,000 s in r59-%s, each distinct genome once (scripts/reads/"
        "r59-d2-pool.py), swum eight to a tank in D173's tank at D177's prices under their own brain, no brain and the leaf smell "
        "knocked out, on 32 held-out episodes. Analysis of a finished round; nothing is searched." % world)
    r['bodies']['pool'] = pool
    del r['bodies']['brains']
    r['search'] = None
    r['exam'].pop('pool', None)
    return r


def bodies_smoke(config_hash):
    """The arm's smoke: two generations of 16, two draws each, the stop read every generation (window 1, over 1, floor 1) so
    that its reading of the reward is exercised, and an exam of two episodes. It checks the build's paths, not the arm."""
    r = bodies_arm(config_hash)
    r['name'] = 'leaf-direction-bodies-smoke'
    r['description'] += " The smoke: two generations of 16 genomes on two draws, the stop read each generation, an exam of two episodes."
    r['search'].update({'population': 16, 'draws': 2, 'generations': 1, 'elites': 2})
    r['search']['stop'] = dict(STOP, measure='reward', window=1, over=1, floor=1)
    r['exam']['episodes'] = 2
    return r


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('wt')
    ap.add_argument('what', choices=['drag', 'smoke', 'arms', 'sparse', 'sparse3', 'tops', 'bodies', 'return', 'repeat', 'patches', 'founders', 'r59d2'])
    ap.add_argument('--world')
    ap.add_argument('--pool')
    ap.add_argument('--form', choices=['Strength', 'Direction'])
    ap.add_argument('--config-hash')
    ap.add_argument('--seed')
    a = ap.parse_args()
    if a.what == 'drag':
        drag(a.wt, a.pool)
    elif a.what == 'sparse':
        write(a.wt, sparse_arm('Strength'))
        write(a.wt, sparse_arm('Direction'))
    elif a.what == 'sparse3':
        write(a.wt, sparse3_arm())
    elif a.what == 'bodies':
        write(a.wt, bodies_arm(a.config_hash))
        write(a.wt, bodies_smoke(a.config_hash))
    elif a.what == 'repeat':
        write(a.wt, return_repeat(a.config_hash, a.seed))
    elif a.what == 'return':
        write(a.wt, return_arm(a.config_hash))
    elif a.what == 'patches':
        write(a.wt, patches_arm(a.config_hash))
        write(a.wt, patches_arm(a.config_hash, '0x2'))
        write(a.wt, pace_bench(a.config_hash, True))
        write(a.wt, pace_bench(a.config_hash, False))
    elif a.what == 'r59d2':
        write(a.wt, r59_d2(a.config_hash, a.world, a.pool))
    elif a.what == 'founders':
        write(a.wt, patches_founders(a.config_hash))
        write(a.wt, patches_founders(a.config_hash, '0x2'))
    elif a.what == 'tops':
        write(a.wt, tops_bench(a.form, a.pool))
    elif a.what == 'arms':
        write(a.wt, run_arm('Strength'))
        write(a.wt, run_arm('Direction'))
    else:
        write(a.wt, arm('Strength', True))
        write(a.wt, arm('Direction', True))
        write(a.wt, arm('Direction', True, 3))
