# Round 48's story film, version 2: the shot list (story.json) and its checks (checks.tsv).
# Reads facts.json and extras.json beside this file (facts.py and extras.py made them from the
# three runs, read-only). Writes only beside this file. Run: python make_story.py
import json, math, os, re

HERE = os.path.dirname(os.path.abspath(__file__))
F = json.load(open(os.path.join(HERE, 'facts.json')))
X = json.load(open(os.path.join(HERE, 'extras.json')))

RUNS = {
    'r48-s1': 'runs/r48-s1/2026-09-24-115115-e5a30c15',
    'r48-s2': 'runs/r48-s2/2026-09-24-115119-e5a30c15',
    'r48-s3': 'runs/r48-s3/2026-09-24-160650-e5a30c15',
}
S1, S2, S3 = RUNS['r48-s1'], RUNS['r48-s2'], RUNS['r48-s3']

# ---------------------------------------------------------------- caption rules
# Since 2026-09-25 a story's captions are set as subtitles in IBM Plex Sans when the film is
# joined (scripts/story-assemble.py), and its charts are drawn in Plex by the theatre, so a
# character is allowed when the font file has it: dashes, curly quotes, superscripts and accents
# all stand. STAMPED = True checks the older rule instead, for a story filmed with its text
# stamped into the frames in the 5x7 bitmap font (theatre-safari.ps1 -BurnText).
STAMPED = False
BITMAP = set(" ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,:;-+=_/()[]%!?'\\*#\u00B7")
MAP = {'\u2014': ' - ', '\u2013': '-', '\u2019': "'", '\u2018': "'", '"': "'", '&': 'and',
       '\u00B2': '2', '\u00B3': '3', '\u00D7': 'x'}
CHARS_PER_SECOND = 14.0   # a reading pace; 4 s holds 56 characters
GAP = 1.0
FIRST = 0.5


def plex_characters():
    """The characters of the repository's IBM Plex Sans, or None when the font or fontTools is missing."""
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return None
    here = HERE
    for _ in range(8):
        path = os.path.join(here, 'unity', 'Assets', 'Theatre', 'UI', 'Fonts', 'IBMPlexSans-Regular.ttf')
        if os.path.isfile(path):
            return set(chr(c) for c in TTFont(path).getBestCmap())
        here = os.path.dirname(here)
    return None


PLEX = None if STAMPED else plex_characters()
if not STAMPED and PLEX is None:
    print('note: IBM Plex Sans (or fontTools) not found: characters are checked only against controls and scripts past U+2E80')


def printable(text):
    """A caption or chart label as it will be shown, and the characters its font lacks."""
    if STAMPED:
        out = ''.join(MAP.get(c, c) for c in text).upper()
        return out, sorted(set(c for c in out if c not in BITMAP))
    if PLEX is not None:
        return text, sorted(set(c for c in text if c not in PLEX))
    import unicodedata
    return text, sorted(set(c for c in text if unicodedata.category(c).startswith('C') or ord(c) >= 0x2E80))


def hold_for(text):
    return max(4.0, math.ceil(len(text) / CHARS_PER_SECOND))


def lay(captions, start=FIRST):
    """Captions one after another: each held long enough to read, one second apart."""
    out, at = [], start
    for c in captions:
        text = c if isinstance(c, str) else c[0]
        h = hold_for(text)
        cap = {'at': at, 'text': text}
        if h != 4.0:
            cap['seconds'] = h
        out.append(cap)
        at = at + h + GAP
    return out, at - GAP  # the end of the last caption


# ---------------------------------------------------------------- checks
CHECKS = []


def check(scene, item, shown, exact, source, how):
    CHECKS.append([str(scene), item, shown, exact, source, how])


# ---------------------------------------------------------------- the readings used
L201 = F['primer_201']
P = L201['price']
E = F['eaters']
LATE = F['late_48048']
POP = F['population']
BETS = F['bets']
POOL = F['pool']
SNOW = F['snow_sources']
VER = F['verdict']
LINES = F['lines']

light_pts = [[d, round(100.0 * math.exp(-d / 6.0), 1)] for d in (0, 3, 6, 9, 12, 15, 18, 24, 30)]

scenes = []


def scene(**k):
    scenes.append(k)
    return k


# ================================================================ chapter 1: how this world works
n = 1
caps, end = lay([
    "This is a tank of water 167 m across, lit from above.",
    "Light fades with depth. At 12 m, about 14% of it is left.",
    "Each body is built from a recipe its children inherit.",
])
scene(n=n, act="How this world works", chapter="How this world works", station="Descent", canopy=True,
      run="r48-s1", second=2500, subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Light at depth, % of the surface", "place": "corner",
             "at": caps[1]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "depth, m"}, "y": {"label": "% of surface light"},
             "series": [{"name": "open water", "points": light_pts, "highlight": True}]},
      why="Opens on the thing everything here lives on, light, and how fast it runs out with depth.",
      sources=["167 m: config/run header radius 83.68 m, 2 x 83.68 = 167.4 m",
               "14% at 12 m: light 200 W/m2 x exp(-depth/6 m) (config light.surfaceIrradiance 200, attenuationDepth 6); exp(-2) = 0.135",
               "recipe: the genome (Genome.cs) is inherited through Mutator"])
check(n, 'caption 1', '167 m across', '167.37 m', S1 + '/config.json', 'world area 22,000 m2 as a disc: r = sqrt(22000/pi) = 83.68 m; 2r = 167.37')
check(n, 'caption 2', 'at 12 m, about 14%', '13.53%', S1 + '/config.json', 'light.surfaceIrradiance 200, light.attenuationDepth 6; LightModel.IrradianceAt = I0 exp(-d/6); exp(-12/6) = 0.1353')
check(n, 'chart', 'light by depth, % of surface', json.dumps(light_pts), S1 + '/config.json', '100 x exp(-d/6) at d = 0..30 m; open water, no reef cap (ReefGeometry.CapTransmission = 0 under a cap) and no crowd shade')

n = 2
caps, end = lay([
    "A child's recipe can change a little. What breeds more, spreads.",
    "Body 201 has two parts: a leaf and a tiny stomach.",
    "A leaf earns energy from light, counted in joules, J.",
    "A stomach eats snow: dead matter drifting in the water.",
    "It pays upkeep to live, and saves what is left: the corner line.",
    "Right now it earns 0.54 J a second and spends 0.49 J.",
])
scene(n=n, act="How this world works", station="Portrait", run="r48-s1", second=2500, flexible=False,
      subject="body 201", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "account", "title": "Body 201's reserve, J", "place": "corner",
             "at": caps[4]['at'], "until": round(end + 0.5, 1)},
      why="Meets one ordinary body and uses it to teach a part, a leaf, a stomach, upkeep and the reserve. Body 201 carries the same kit as the story's last body, 48048, so the film opens and closes on it.",
      sources=["body 201: lineage born 523 s to parent 119 (Frondium marufubens), bud absorptive, died 3,206.5 s; alive at the 2,500 s checkpoint",
               "two parts, a leaf and a stomach: absorptive.jsonl t=2500 parts 2, snapshot 2500 nodes photosynthetic + absorptive",
               "tiny stomach: absVolume 0.000199 of volume 0.0821 m3 = 0.24%",
               "0.54 and 0.49: absorptive.jsonl t=2500 lightW 0.539 + foodW 0.0018; upkeepW 0.409 + exudedW 0.081"])
check(n, 'caption 1', 'a recipe can change; what breeds more spreads', 'rule', 'src/Evosim.Core (Mutator, World.Conceive)', 'a child is conceived from a mutated copy of the parent genome; nothing else selects')
check(n, 'caption 2', 'two parts: a leaf and a tiny stomach', 'parts 2; absVolume/volume 0.00242', S1 + '/absorptive.jsonl', 'row id 201 t 2500: parts 2, photoArea 0.305, absVolume 0.000199, volume 0.0821; snapshot 2500 genome nodes [photosynthetic, absorptive]')
check(n, 'caption 3', 'a leaf earns energy from light', 'rule', 'StandardCellTypes.cs Fixation.Fix', 'a leaf fixes light x lit area x efficiency 0.05, capped by what dissolved matter it can take up')
check(n, 'caption 4', 'a stomach eats snow', 'rule', 'StandardCellTypes.cs AbsorptiveCell; World.Metabolise', 'food = clearance 10 x stomach volume x snow density at the body (3318 at 3,500 s: 10 x 0.0899 x 1.393 = 1.252 W, its foodW 1.252)')
check(n, 'caption 5', 'upkeep; saves the rest', 'rule', 'Metabolism.cs Bill; World.Metabolise', 'net = light + food - upkeep - leak; the net goes to the reserve (Organism.Energy)')
check(n, 'caption 6', 'earns 0.54 J a second', '0.5410 W', S1 + '/absorptive.jsonl', 'id 201 t 2500: lightW 0.53922 + foodW 0.00182')
check(n, 'caption 6', 'spends 0.49 J', '0.4898 W', S1 + '/absorptive.jsonl', 'id 201 t 2500: upkeepW 0.40896 + exudedW 0.08088; netW 0.05119')
check(n, 'chart', 'account: body 201 reserve, live', '44.64 J at 2,500 s (the run)', S1 + '/absorptive.jsonl', 'drawn live from the cousin; the run read energy 44.636 at t 2500')

n = 3
life = X['life_201']
life_pts = [[r[0], r[1]] for r in life]
caps, end = lay([
    "The life of body 201",
    "Born small, it put its savings into growing.",
    "Grown, it saved for its children's price plus a cushion.",
    "At 850 s it had two, for 129 J. The 14 J cushion was left.",
    "Upkeep rises with age, and it drifted down into dimmer light.",
    "At zero reserve a body dies. Body 201 starved at 3,207 s.",
])
scene(n=n, act="How this world works", station="Card", description="a title card: a slow drift through the crowd, under a full chart",
      run="r48-s1", second=2500, subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Body 201's reserve over its life, J", "place": "full",
             "at": caps[1]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "reserve, J"},
             "series": [{"name": "body 201", "points": life_pts, "highlight": True}],
             "marks": [{"x": 550, "label": "full grown"}, {"x": 850, "label": "two children"},
                       {"x": 2500, "label": "we met it here"}, {"x": 3206.5, "label": "starved"}]},
      why="Answers two of the owner's questions with one line: what it takes to have a child, and what it takes to die.",
      sources=["absorptive.jsonl, every row of id 201 from 530 to 3,206.5 s: energy (reserve), tissue, y",
               "full grown at 550 s: tissue 41.034 J from t 550 on (25.5 J at 530)",
               "850 s: lineage children 402 and 403 born 850; reserve 139.05 J at 840, 14.00 J at 850",
               "cushion: genome reserve margin 51.21 s x standing watts 0.2736 at 850 s = 14.0 J",
               "died 3,206.5 s starved, at 15.0 m (y -15.02); at 2,500 s it was at 3.2 m"])
check(n, 'caption 2', 'born small (bf 0.405), savings into growing', 'rule + reading', 'World.cs Grow (before Reproduce in Step); config growth.growthReserveFloor 0.1', 'a body below adult size spends all reserve above 10% of its tissue on growth each step; 201: tissue 25.5 J at 530 s, 41.03 J (adult) at 550 s, reserve 2.5 -> 15.6 J')
check(n, 'caption 3', "saved for its children's price plus a cushion", 'rule', 'World.cs Conceive / IsSolvent', 'a lump breeder conceives when reserve >= price of the litter + ReserveMargin x StandingWatts')
check(n, 'caption 4', 'at 850 s it had two', '850 s, ids 402 and 403', S1 + '/lineage.jsonl', "birth rows p=201: 402 at 850, 403 at 850")
check(n, 'caption 4', 'for 129 J', '129.37 J (price); drop 129.3 J', S1 + '/absorptive.jsonl + snapshot 2500', 'price per child = share 14.687 (investment 0.7159 x tissue 41.034 / brood 2) + fee 50 = 64.69 J; x2 = 129.37; reserve 139.054 at 840 + 10 s x ~0.43 W = 143.3, 14.004 at 850')
check(n, 'caption 4', 'the 14 J cushion', '14.004 J left; margin 51.21 s x 0.2736 W = 14.01 J', S1 + '/absorptive.jsonl + snapshot 2500', 'energy at t 850 = 14.0037; genome reproduction.margin 51.2128; upkeepW at 850 0.2736')
check(n, 'caption 5', 'upkeep rises with age', 'wear 1 + age/3000', 'Metabolism.cs; config senescence 3000 s; D121', '201 upkeep 0.2736 W at age 327 s, 0.409 at 1,977, 0.467 at 2,683.5; /(1+age/3000) = 0.247, 0.247, 0.247')
check(n, 'caption 5', 'drifted down into dimmer light', 'y -3.16 m at 2,500 s, -13.81 at 3,000, -15.02 at death; lightW 0.539 -> 0.265 -> 0.226', S1 + '/absorptive.jsonl', 'rows id 201 t 2500, 3000, 3206.5. Why it sank is not read: the current carries bodies in three dimensions (D088, D090), which is a guess at the cause')
check(n, 'caption 6', 'starved at 3,207 s', '3,206.5 s, cause starved', S1 + '/lineage.jsonl', "death row id 201 t 3206.5 c 'starved'; energy 0 in its last absorptive row")
check(n, 'chart', 'reserve over its life', '%d points, t 530..3206.5' % len(life_pts), S1 + '/absorptive.jsonl', 'every row with id 201: [t, energy]; values: ' + json.dumps(life_pts))
check(n, 'chart marks', 'full grown 550; two children 850; we met it here 2500; starved 3206.5', '550, 850, 2500, 3206.5', S1 + '/absorptive.jsonl; lineage.jsonl', 'tissue reaches 41.034 at t 550; births of 402/403 at 850; the Portrait scene second; death row')

n = 4
caps, end = lay([
    "What one child cost",
    "The parent gives each child a share of its own body's worth.",
    "Four fifths build the child's body. One fifth is its first reserve.",
    "On top, a fee of twice the child's body, at least 50 J, is burnt.",
])
scene(n=n, act="How this world works", station="Card", description="a title card: a slow drift through the crowd, under a full chart",
      run="r48-s1", second=2500, subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "bars", "title": "One child of body 201, J", "place": "full",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "bars": [{"label": "the child's body", "value": round(P['child_body_J'], 1)},
                      {"label": "its first reserve", "value": round(P['child_reserve_J'], 1)},
                      {"label": "the fee, burnt", "value": round(P['fee_per_child_J'], 1), "highlight": True}]},
      why="The owner asked what it takes to have a child. This is the price, in the parts the code charges.",
      sources=["share = BirthInvestment 0.7159 x tissue 41.034 J / brood 2 = 14.69 J (snapshot 2500, absorptive.jsonl)",
               "reserve = 0.2 of the share (growth.newbornReserveFraction), body = 0.8",
               "fee = max(50 J, 2 x the child's tissue) (economy.perOffspringOverheadJoules 50, perOffspringOverheadPerTissueJoule 2; RunConfig.OverheadFor)"])
check(n, 'caption 2', "a share of its own body's worth", 'share = investment x tissue / brood = 0.7159 x 41.034 / 2 = 14.69 J', 'snapshot 2500 + absorptive.jsonl id 201; World.Conceive', 'BirthInvestment x parent TissueJoules / BroodSize, capped at the child\'s adult / 0.8')
check(n, 'caption 3', 'four fifths body, one fifth reserve', 'newbornReserveFraction 0.2', S1 + '/config.json; World.Conceive', 'reserve = share x 0.2; body value = share - reserve')
check(n, 'caption 4', 'fee twice the child\'s body, at least 50 J, burnt', 'max(50, 2 x tissue)', S1 + '/config.json; RunConfig.OverheadFor; World.Conceive', 'economy.perOffspringOverheadJoules 50, perOffspringOverheadPerTissueJoule 2; the overhead leaves as heat (EnergyOut) and its matter returns dissolved')
check(n, 'chart', "the child's body", '%.3f J' % P['child_body_J'], 'snapshot 2500 + absorptive.jsonl id 201', 'share 14.687 x 0.8')
check(n, 'chart', 'its first reserve', '%.3f J' % P['child_reserve_J'], 'snapshot 2500 + absorptive.jsonl id 201', 'share 14.687 x 0.2')
check(n, 'chart', 'the fee, burnt', '50 J', S1 + '/config.json', 'max(50, 2 x 11.75 = 23.5) = 50')

# ================================================================ chapter 2: what we hoped for
n = 5
caps, end = lay([
    "Round 48 is one experiment: three tanks under the same rules.",
    "Each runs 30,000 s, about eight hours. This is tank 3 at 3,000 s.",
    "COUSIN in the corner: each scene is re-run from a saved moment.",
])
scene(n=n, act="What we hoped for", chapter="What we hoped for", station="Arrival", run="r48-s3", second=3000,
      subject="world", seconds=round(end + 0.5, 1), captions=caps,
      why="Says what a round and a tank are, and what the COUSIN mark means, before any caption says 'in the run'. 3,000 s is a checkpoint of seed 3.",
      sources=["three tanks: r48-s1, r48-s2, r48-s3, one config hash e5a30c15 across the three run directories",
               "COUSIN: the theatre's provenance word for a scene stepped live in the Editor from a checkpoint (CLAUDE.md, live play)"])
check(n, 'caption 1', 'three tanks, the same rules', 'config hash e5a30c15 in all three run directory names', 'runs/r48-s*/*-e5a30c15', 'the three seeds of round 48')
check(n, 'caption 2', '30,000 s, about eight hours', '30,000 s = 8.33 h', 'runs/r48-s1, r48-s3 run.json', "the budget; seed 2 stopped at 13,700 s (said in scene 19)")
check(n, 'caption 2', 'tank 3 at 3,000 s', 'r48-s3 t 3000', S3, 'the scene second, a checkpoint (every 500 s in seed 3)')

n = 6
s3snow = SNOW['r48-s3']
caps, end = lay([
    "Most snow leaks from living leaves: 15% of what they catch.",
    "The rest comes from the dead. A corpse crumbles into snow.",
])
scene(n=n, act="What we hoped for", station="Floor", run="r48-s3", second=3000, subject="world",
      seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "bars", "title": "Tank 3's snow over its whole run, %", "place": "corner",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "bars": [{"label": "leaked by leaves", "value": round(100 * s3snow['leaked_share'], 1), "highlight": True},
                      {"label": "from the dead", "value": round(100 * s3snow['dead_share'], 1)}]},
      why="Corrects the first film, which said the dead make the snow. Three quarters of it is leaked by living leaves.",
      sources=["stats.jsonl t=30000 detritusExudedTotal %.0f J (leaked by the living) and detritusDepositedTotal %.0f J (corpses and shed parts); detritusReturnedTotal %.0f J (feeders' waste)" % (s3snow['leaked_J'], s3snow['dead_J'], s3snow['waste_J']),
               "15%: feeding.exudationFraction 0.15; 201 at 2,500 s leaked 0.0809 of 0.539 W light",
               "a corpse decays at 0.5%/s (corpseDecayPerSecond 0.005, half in 139 s)",
               "snow sinks 2 mm/s; remineralises 0.05%/s back into dissolved matter"])
check(n, 'caption 1', 'most snow leaks from living leaves', '75.9% of snow joules over the whole run (s1 75.4%, s2 73.8%)', S3 + '/stats.jsonl', 'last row: detritusExudedTotal / (Exuded + Deposited + Returned)')
check(n, 'caption 1', '15% of what they catch', 'exudationFraction 0.15', S3 + '/config.json', 'feeding.exudationFraction; 201 at 2,500 s: exudedW 0.08088 / lightW 0.53922 = 0.150')
check(n, 'caption 2', 'the rest comes from the dead', '24.1%', S3 + '/stats.jsonl', 'detritusDepositedTotal / total; Deposited counts corpse instalments and shed parts (World.cs, WorldModules.cs)')
check(n, 'caption 2', 'a corpse crumbles into snow', 'corpseDecayPerSecond 0.005', S3 + '/config.json', 'World.StepCorpses; half-life ln2/0.005 = 139 s')
check(n, 'chart', 'leaked by leaves', '%.2f%%' % (100 * s3snow['leaked_share']), S3 + '/stats.jsonl', 'detritusExudedTotal %.1f J at t 30000' % s3snow['leaked_J'])
check(n, 'chart', 'from the dead', '%.2f%%' % (100 * s3snow['dead_share']), S3 + '/stats.jsonl', 'detritusDepositedTotal %.1f J at t 30000; feeders\' waste %.1f J (%.3f%%) not drawn' % (s3snow['dead_J'], s3snow['waste_J'], 100 * s3snow['waste_share']))

n = 7
leg3000 = BETS['r48-s3']['counts']['Thallus legatribens'][BETS['r48-s3']['times'].index(3000)]
caps, end = lay([
    "A line is a family of bodies with the same kinds of part.",
    "These leaves are one line, Thallus legatribens: %d alive." % leg3000,
    "The names are made up. The first word tells the kind of body.",
    "A child that gains or loses a leaf, stomach or joint starts a new line.",
    "An eater is a body with a stomach and no leaf. It lives on snow.",
    "Last round, no line of eaters lasted.",
    "So we changed four rules in the eaters' favour.",
    "We hoped for a line of eaters, ten strong at the end.",
    "Or at least a leaf that grew a stomach and kept it.",
])
scene(n=n, act="What we hoped for", station="Colony", run="r48-s3", second=3000,
      subject="Thallus legatribens (guide clade 52), founder body 63", seconds=round(end + 0.5, 1), captions=caps,
      why="States the hope, and defines an eater and a line over a line the viewer can see (the leaf line that later wins tank 3). Each of the four rules is explained later, at the scene where it matters.",
      sources=["line: the guide's clade rule (scripts/guide.py build_clades): a child whose abs, jnt and pho flags equal its parent's joins the parent's clade, otherwise founds one",
               "Thallus legatribens at 3,000 s: lineage clade walk, clade 63, %d alive; guide index 52, founded 139 s by a child that dropped its parent's joint" % leg3000,
               "names: scripts/guide.py GENERA, the genus from the flags, the epithet from a hash of the founder's genome",
               "last round: logbook/0119 F3 row, round 47 0 of 3",
               "four rules: D119 to D122 (logbook/0119 'What it asks')",
               "ten strong: logbook/0119 predictions M3 and F3 (ten or more living at 30,000 s in 1 of 3)"])
check(n, 'caption 5', 'an eater: a stomach and no leaf', 'flags abs 1, pho 0', 'scripts/guide.py GENERA', "the guide's stomach genera (Gastrella, Phagus, Voratrix, Stomachium) are abs 1 pho 0 jnt 0; this film calls those eaters")
check(n, 'caption 1', 'a line: a family with the same kinds of part', 'rule', 'scripts/guide.py build_clades', 'clade membership by equal (abs, jnt, pho) flags along the parent chain')
check(n, 'caption 2', 'Thallus legatribens, %d alive' % leg3000, str(leg3000), S3 + '/lineage.jsonl', 'clade 63 (guide rule) alive at 3000; flags (0,0,1), leaves')
check(n, 'caption 3', 'the first word tells the kind of body', 'rule', 'scripts/guide.py GENERA', 'leaf: Phyllina, Thallus, Lamina, Frondium; eater: Gastrella, Phagus, Voratrix, Stomachium; leaf+stomach: Mixophyllum, Gastrophylla, Phagothallus, Vorafrons; jointed eater: Remiphagus, Nectophaga, Pinnivora, Natogastrum; jointed leaf: Remiphylla, Nectothallus, Pinnifrons, Natophyllum; a Roman numeral, then a number past X, marks a repeated name')
check(n, 'caption 4', 'gains or loses a leaf, stomach or joint: a new line', 'rule', 'scripts/guide.py build_clades', 'a child whose (abs, jnt, pho) differ from its parent founds a clade')
check(n, 'caption 6', 'last round, no line of eaters lasted', 'round 47 F3: 0 of 3', 'logbook/0119-a-stomach-on-a-plant-and-paying-as-you-go.md', 'F3 row: round 47 0 of 3')
check(n, 'caption 7', 'four rules', 'D119, D120, D121, D122', 'DECISIONS.md', 'the bud; gestation and the scaled fee; senescence on upkeep alone; founders at their food with an endowment')
check(n, 'caption 8', 'a line of eaters, ten strong at the end', '>= 10 living at 30,000 s in 1 of 3', 'logbook/0119 F3', 'ten or more living bodies rooted in pool founders at 30,000 s')
check(n, 'caption 9', 'a leaf that grew a stomach and kept it', ">= 10 living in a budded stomach's line at 30,000 s in 1 of 3", 'logbook/0119 M3', 'a connected clade whose first member carries an absorptive bud (abs 1, pho 1)')

# ================================================================ chapter 3: the eaters arrive
n = 8
r3318 = E['rows']['3318@3500']
land = E['landing']['3318']
grow_cost = 44.951958 - land['bf'] * 44.951958
share = 0.41533542 * 44.951958 / 1
child_body = 0.8 * share
child_price = share + 50.0
caps, end = lay([
    "Body 3318 is a newcomer: an eater copied from past rounds.",
    "Its reserve is in the corner. In the run it bred 11.5 s in.",
    "Random newcomers drop in every 30 s or so. One in ten is a stored eater.",
    "It eats 1.25 J a second and pays 0.50 J of upkeep.",
    "New rule four: a newcomer lands at the richest depth for its food.",
    "It landed 157 s ago, with 66 J of its own and a gift of 72 J.",
    "Half a second later it was full grown and had a child.",
    "That cost 99 J, more than its own 66 J. The gift paid.",
])
scene(n=n, act="The eaters arrive", chapter="The eaters arrive", station="Portrait", run="r48-s3", second=3500,
      flexible=False, subject="founder body 3318", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "account", "title": "Body 3318's reserve, J", "place": "corner",
             "at": caps[0]['at'], "until": round(end + 0.5, 1)},
      why="The first eater of the story, at a checkpoint where it is 156.5 s old and about to breed. Rule four and the gift are explained on the body they helped.",
      sources=["lineage: 3318 born 3,343.5 s, kind f, src pool, pool 0, bf 0.3323, endow 71.69 J; child 3319 at 3,344 s; child 3392 at 3,511.5 s",
               "absorptive.jsonl id 3318 t 3500: age 156.5, parts 1, lightW 0, foodW 1.252, upkeepW 0.5035, energy 94.16",
               "purse 200 J x bf 0.3323 = 66.45 J (population.founderEnergyJoules 200)",
               "growth 44.952 - 14.936 = 30.0 J; child = share 18.67 (0.4153 x 44.952) + fee 50 = 68.67 J"])
check(n, 'caption 3', 'newcomers about every 30 s (random founders)', 'foundingTricklePerSecond 0.0333', S3 + '/config.json', 'Poisson trickle after the floor closes at 3,000 s (floorClosesAfterSeconds 3000)')
check(n, 'caption 3', 'one in ten is a stored eater', 'foundingTricklePoolShare 0.1, pool of 4', S3 + '/config.json', 'World.PoolFounder; the four pool genomes are stomach bodies (logbook/0119 ledger table)')
check(n, 'caption 1', 'body 3318 is a newcomer, an eater copied from past rounds (one part, no leaf, on screen)', 'parts 1, absVolume = volume 0.0899 m3, lightW 0', S3 + '/absorptive.jsonl', 'row id 3318 t 3500; lineage abs 1 jnt 0 pho 0; pool index 0 (one-part sphere)')
check(n, 'caption 2', 'bred 11.5 s in', 'child 3392 born 3,511.5 s', S3 + '/lineage.jsonl', '3511.5 - 3500 = 11.5; gate: reserve 94.16 + 0.749 W x 11.5 s = 102.8 >= 18.67 + 50 + 88.28 s x 0.378 W = 102.1')
check(n, 'caption 4', 'eats 1.25 J a second, pays 0.50 J', 'foodW 1.2520, upkeepW 0.5035', S3 + '/absorptive.jsonl', 'row id 3318 t 3500 (upkeepW includes handling at 0.1 J per J eaten)')
check(n, 'caption 5', 'rule four: lands at the richest depth for its food', 'D116, D122', 'DECISIONS.md; World.EnsureFounderAcceptance / FoodOf', 'a column is accepted with probability its stock over the richest column\'s; the founder is placed at that column\'s richest layer. F4 read only 19 to 21% of pool founders first fed at or above their column\'s mean (logbook r48 read), so this is the rule, not a measured outcome')
check(n, 'caption 6', 'landed 157 s ago', 'age 156.5 s', S3 + '/absorptive.jsonl', 'row id 3318 t 3500')
check(n, 'caption 6', '66 J of its own', '66.454 J', S3 + '/lineage.jsonl + config', 'founderEnergyJoules 200 x bf 0.33226845')
check(n, 'caption 6', 'a gift of 72 J', '71.693 J', S3 + '/lineage.jsonl', "founder row 'endow' 71.6934 (600 s x its standing watts at its landing size)")
check(n, 'caption 7', 'half a second later: full grown (30 J of growth)', '%.2f J' % grow_cost, S3 + '/absorptive.jsonl + lineage', 'adult tissue 44.952 (rows from 3350) - landing tissue 0.33227 x 44.952 = 14.936')
check(n, 'caption 7', 'and had a child (3319 at 3,344 s; 69 J)', '%.2f J' % child_price, S3 + '/lineage.jsonl + snapshot 3500', 'child 3319 born 3,344 s, bf 0.33227 (adult 44.952 at 3370); share = investment 0.41534 x 44.952 / brood 1 = 18.67; body 14.94 + reserve 3.73 + fee max(50, 29.9) = 50')
check(n, 'caption 8', 'that cost 99 J, more than its own 66 J; the gift paid', '30.02 + 68.67 = 98.69 J > its own 66.45 J', 'the two rows above', 'arithmetic; with the gift, 138.15 J >= 98.69 J')
check(n, 'chart', 'account: body 3318, live', '94.16 J at 3,500 s (the run)', S3 + '/absorptive.jsonl', 'drawn live from the cousin')

n = 9
bars_life = []
for tank, arm, r47 in (('tank 1', 'r48-s1', 46), ('tank 2', 'r48-s2', 24), ('tank 3', 'r48-s3', 42)):
    bars_life.append({"label": tank + ", last round", "value": r47})
    bars_life.append({"label": tank + ", this round", "value": round(POOL[arm]['median_life_s']), "highlight": True})
caps, end = lay([
    "Its line, Gastrella cidutis, has 24 alive, 11 minutes on.",
    "This round, a typical stored eater lived about five minutes.",
    "Last round, under one minute. And 56 of them bred.",
])
scene(n=n, act="The eaters arrive", station="Colony", run="r48-s3", second=4000,
      subject="Gastrella cidutis (guide clade 362), founder body 3318", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "bars", "title": "A stored eater's typical life, s", "place": "corner",
             "at": caps[1]['at'], "until": round(end + 0.5, 1), "bars": bars_life},
      why="The first eater line at its size in tank 3 (it peaked at 26 at 4,049 s), and the first half of the round's hope coming true: stored eaters now live and breed.",
      sources=["24: lineage clade walk (guide rule), clade of 3318 alive at t=4000",
               "11 minutes: 4,000 - 3,343.5 = 656.5 s",
               "median life: lineage, pool founders' age at death (s1 312, s2 297.75, s3 335 s); round 47: logbook/0119 F1",
               "56 bred: pool founders with a child, 30 + 8 + 18"])
check(n, 'caption 1', '24 alive', '24', S3 + '/lineage.jsonl', 'clade of 3318 (guide rule) alive at 4000; every one of its 30 members ever had flags (1,0,0)')
check(n, 'caption 1', '11 minutes on', '656.5 s', S3 + '/lineage.jsonl', '4000 - 3343.5')
check(n, 'caption 2', 'a typical stored eater lived about five minutes (median)', '312, 297.75, 335 s', 'runs/r48-s*/lineage.jsonl', "pool founders (src 'pool') that died, median age at death; s1 99 arrived 97 died, s2 29/28, s3 84/84")
check(n, 'caption 3', 'last round under one minute', '46, 24, 42 s', 'logbook/0119 F1 row', 'round 47')
check(n, 'caption 3', '56 of them bred', '30 + 8 + 18', 'runs/r48-s*/lineage.jsonl', 'pool founders with at least one child; 54 of the 56 were pool index 0, the one-part sphere')
for b in bars_life:
    check(n, 'chart', b['label'], str(b['value']) + ' s', 'r47: logbook/0119 F1; r48: lineage.jsonl', 'median age at death of pool founders')

n = 10
caps, end = lay([
    "Tank 1 drew the same stored eater at 4,057 s. It bred at once too.",
    "Its line is Gastrella cidutis III, the third of that name here.",
    "In the run it reached 45, the largest eater line of any tank.",
    "The same tank had a jointed eater line, Natogastrum plocrele.",
    "A joint lets two parts swing, driven by the body's small brain.",
])
scene(n=n, act="The eaters arrive", station="Colony", run="r48-s1", second=6000, flexible=False,
      subject="Gastrella cidutis III (guide clade 180), founder body 4185", seconds=round(end + 0.5, 1), captions=caps,
      why="The same stored recipe in another tank, so it was not luck; the numeral in a name; and the high point of the eaters' story, the largest eater line of the round at its peak.",
      sources=["lineage: 4185 born 4,057 s, src pool, pool 0, the same bf and endow as 3318; child 4186 at 4,057.5 s",
               "guide.json: Gastrella cidutis (founder 3445, 3,336.5 s), II (3456, 3,345 s), III (4185, 4,057 s)",
               "45: lineage clade of 4185 alive at 6,000 s; peak 45 at 5,903.5 s",
               "largest eater line: the largest clade with flags (1,0,0) in any tank, s1 45, s2 2, s3 26",
               "Natogastrum plocrele: founder 3575 (trickle, 3,462.5 s), flags abs 1 jnt 1 pho 0, 2 parts; peak 14 at 7,391.5 s",
               "joint: jnt = the body has a degree of freedom; only a link part carries a moving joint, and the brain's neurons live on links"])
check(n, 'caption 1', 'the same stored eater at 4,057 s', 'pool index 0 in both; born 4057', S1 + '/lineage.jsonl', "founder rows 4185 (s1) and 3318 (s3) both 'pool': 0, bf 0.33226845, endow 71.6934")
check(n, 'caption 1', 'it bred at once too', 'child 4186 at 4,057.5 s', S1 + '/lineage.jsonl', 'birth row p=4185; half a second after landing, as 3318 in tank 3')
check(n, 'caption 2', 'the third of that name here', 'III', S1 + '/guide/guide.json', "cards 'Gastrella cidutis' (3,336.5 s), 'II' (3,345 s), 'III' (4,057 s); the guide numbers lines that share a name in founding order")
check(n, 'caption 3', 'reached 45', '45 at 5,903.5 s (45 at 6,000 s)', S1 + '/lineage.jsonl', 'event sweep of clade 4185; count at 6000 = 45')
check(n, 'caption 3', 'the largest eater line of any tank', 's1 45, s2 2, s3 26', 'runs/r48-s*/lineage.jsonl', 'largest clade with flags (1,0,0), whole runs (first story checks.tsv row 9, re-read)')
check(n, 'caption 4', 'the same tank had a jointed eater line, Natogastrum plocrele', 'founder 3575, trickle, 3,462.5 s; peak 14 at 7,391.5 s (on the chart in scene 12)', S1 + '/lineage.jsonl', 'event sweep of clade 3575; flags abs 1, jnt 1, pho 0, pt 2; at 5,000 s it also earned 0.349 W of light through its link (links photosynthesise at 0.025, half a leaf)')
check(n, 'caption 5', 'a joint driven by the brain', 'rule', 'StandardCellTypes.cs LinkCell; Brain.cs', 'the link is the only type that may carry a moving joint; the brain drives the joints')

# ================================================================ chapter 4: the eaters fade
n = 11
r3559 = E['rows']['3559@6500']
caps, end = lay([
    "Tank 3, 6,500 s. Body 3559 is the last of Gastrella cidutis.",
    "It is 2,655 s old. Upkeep rises with age, doubling by 3,000 s.",
    "It pays 0.73 J a second. Young, the line's first body paid 0.50 J.",
    "The snow here is thinner. It eats 0.55 J a second.",
    "Its reserve is 9 J, and falling.",
    "In the run it starved 44.5 s from here, and the line ended.",
    "No living body was eaten this round. Every death was starving.",
])
scene(n=n, act="The eaters fade", chapter="The eaters fade", station="Portrait", run="r48-s3", second=6500,
      flexible=False, subject="body 3559", seconds=46, captions=caps,
      chart={"kind": "account", "title": "Body 3559's reserve, J", "place": "corner", "at": 0.5, "until": 46},
      why="Rule three and death shown on one body: an old eater in thinner snow, its reserve running out on screen. 6,500 s is a checkpoint of seed 3. The scene runs 46 s so that the recorded death (44.5 s in) falls inside it; in the cousin it may come a little earlier or later.",
      sources=["lineage: 3559 born 3,845.5 s, died 6,544.5 s starved; founder of its line 3318; at 6,500 s the only living member",
               "absorptive.jsonl id 3559 t 6500: age 2654.5, foodW 0.547, upkeepW 0.7325, energy 9.256, densityHere 0.609",
               "3318 at 3,500 s: upkeepW 0.5035, densityHere 1.393",
               "senescence: upkeep x (1 + age / 3000 s), D121; config senescence 3000",
               "stats.jsonl end rows: bodiesEaten 0 in all three; deaths all 'starved'"])
check(n, 'caption 1', 'the last of Gastrella cidutis', 'line alive at 6500 = [3559]; last death 6,544.5', S3 + '/lineage.jsonl', 'clade of 3318')
check(n, 'caption 2', '2,655 s old', '2,654.5 s', S3 + '/absorptive.jsonl', 'row id 3559 t 6500')
check(n, 'caption 2', 'doubling by 3,000 s', 'wear = 1 + age/3000', 'D121; Metabolism.cs; config senescence doubling 3000 s', 'at age 3,000 s the factor is 2')
check(n, 'caption 3', 'pays 0.73 J a second', '0.7325 W', S3 + '/absorptive.jsonl', 'upkeepW, including handling 0.1 x 0.547 = 0.055 W')
check(n, 'caption 3', "young, the line's first body paid 0.50 J", '0.5035 W at age 156.5 s', S3 + '/absorptive.jsonl', 'row id 3318 t 3500 (handling 0.125 W of it); standing costs 0.678 and 0.378 W, ratio 1.79 = (1+2654.5/3000)/(1+156.5/3000)')
check(n, 'caption 4', 'the snow here is thinner', 'densityHere 0.609 J/m3 against 1.393 for 3318 at 3,500 s', S3 + '/absorptive.jsonl', 'rows 3559@6500 and 3318@3500')
check(n, 'caption 4', 'eats 0.55 J a second', '0.5472 W', S3 + '/absorptive.jsonl', 'foodW')
check(n, 'caption 5', 'reserve 9 J', '9.256 J', S3 + '/absorptive.jsonl', 'energy; netW -0.185')
check(n, 'caption 6', 'starved 44.5 s from here', '6,544.5 - 6,500', S3 + '/lineage.jsonl', "death row id 3559 t 6544.5 c 'starved'")
check(n, 'caption 7', 'no living body eaten', 'bodiesEaten 0, 0, 0', 'runs/r48-s*/stats.jsonl', 'last rows (corpsesEaten 3, 5, 2: a few corpses were bitten)')
check(n, 'caption 7', 'every death was starving', 'starved 67,036 / 22,504 / 47,414; nothing else', 'runs/r48-s*/lineage.jsonl', "death rows' cause 'c'; diverged 0 in all three")
check(n, 'chart', 'account: body 3559, live', '9.26 J at 6,500 s (the run)', S3 + '/absorptive.jsonl', 'drawn live from the cousin')

n = 12
T = E['times']
ser = [{"name": "Gastrella cidutis, tank 3", "points": [[t, c] for t, c in zip(T, E['r48-s3']['Gastrella cidutis'])], "highlight": True},
       {"name": "Gastrella cidutis III, tank 1", "points": [[t, c] for t, c in zip(T, E['r48-s1']['Gastrella cidutis III'])]},
       {"name": "Natogastrum plocrele, tank 1", "points": [[t, c] for t, c in zip(T, E['r48-s1']['Natogastrum plocrele'])]}]
caps, end = lay([
    "Every eater line died out",
    "The last, the jointed one, was gone by 10,912 s.",
    "No stored eater had a line alive at the end.",
    "I don't know why the eater lines faded.",
    "My guess: as they aged, they thinned the snow around them.",
])
scene(n=n, act="The eaters fade", station="Card", description="a title card: a slow drift through the crowd, under a full chart",
      run="r48-s3", second=6500, subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Eater lines, bodies alive", "place": "full",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "alive"}, "series": ser,
             "marks": [{"x": 6544.5, "label": "body 3559 starves"}]},
      why="The obstacle, in one picture: three eater lines rise and fall. The why is marked as a guess.",
      sources=["counts: lineage event sweep every 100 s from 3,000 to 11,000 s, clades 3318 (s3), 4185 and 3575 (s1)",
               "10,912 s: guide.json Natogastrum plocrele extinct_at 10912",
               "no line alive at the end: bodies rooted in pool founders alive at each end are pool founders themselves (s1 63980, 74343; s2 26745; s3 none)",
               "the guess rests on two bodies: 3318 fed at 1.39 J/m3 at 156 s old, 3559 at 0.61 at 2,655 s old"])
check(n, 'caption 2', 'gone by 10,912 s', '10,912 s', S1 + '/guide/guide.json; lineage.jsonl', 'last death in clade 3575')
check(n, 'caption 3', 'no stored eater had a line alive at the end', 'living pool-rooted bodies at the ends are founders only', 'runs/r48-s*/lineage.jsonl', 'parent walk to a pool founder, alive at the last sample; F3 0 of 3')
check(n, 'caption 5', 'thinned the snow around them (guess)', '1.39 against 0.61 J/m3', S3 + '/absorptive.jsonl', 'two rows only; not a measurement across the lines')
for s in ser:
    check(n, 'chart', s['name'], 'peak %s' % max(p[1] for p in s['points']), 'lineage.jsonl', 'counts at 3000..11000 every 100 s: ' + json.dumps([p[1] for p in s['points']]))
check(n, 'chart mark', 'body 3559 starves', '6,544.5 s', S3 + '/lineage.jsonl', 'death row id 3559')

# ================================================================ chapter 5: two leaves, two bets
n = 13
BS = F['birth_sizes']
caps, end = lay([
    "Tank 1, 10,000 s. Two leaf lines hold nearly half each.",
    "Frondium marufubens bets on two big children at a time.",
    "Frondium nisimocrax bets on one small child.",
    "Their children start at 38% and 6% of adult size.",
])
scene(n=n, act="Two leaves, two bets", chapter="Two leaves, two bets", station="Colony", run="r48-s1", second=10000,
      subject="Frondium marufubens (guide clade 38), founder body 39", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "bars", "title": "A child's size at birth, % of adult", "place": "corner",
             "at": caps[2]['at'], "until": round(end + 0.5, 1),
             "bars": [{"label": "marufubens", "value": round(100 * BS['r48-s1']['Frondium marufubens']['median_bf'], 1), "highlight": True},
                      {"label": "nisimocrax", "value": round(100 * BS['r48-s1']['Frondium nisimocrax']['median_bf'], 1)}]},
      why="Meanwhile, the leaves. The line that wins tank 1 is the one whose leaf later grows the stomach that lasts (chapter 6).",
      sources=["1,315 and 1,386 of 2,859 alive at 10,000 s (lineage clade walk; stats alive 2859)",
               "snapshot 10,000: marufubens median investment 0.911 brood 2 (n 1,315); nisimocrax 0.0703 brood 1 (n 1,386)",
               "birth sizes: lineage 'bf' median of births in each line, 5,000 to 15,000 s: 0.385 (5,077 births) and 0.056 (11,060)"])
check(n, 'caption 1', 'nearly half each', '1,315 (46%) and 1,386 (48%) of 2,859', S1 + '/lineage.jsonl; stats.jsonl', 'clades 39 and 43 at t 10000')
check(n, 'caption 2', 'two big children at a time', 'median investment 0.911, brood 2', S1 + '/snapshots/000010000.jsonl', 'members of clade 39')
check(n, 'caption 3', 'one small child', 'median investment 0.0703, brood 1', S1 + '/snapshots/000010000.jsonl', 'members of clade 43')
check(n, 'caption 4', '38% and 6% of adult size', '0.3847 and 0.0563', S1 + '/lineage.jsonl', "median 'bf' of birth rows in each clade, 5,000 to 15,000 s")
check(n, 'chart', 'marufubens', '38.5%', S1 + '/lineage.jsonl', 'as caption 4')
check(n, 'chart', 'nisimocrax', '5.6%', S1 + '/lineage.jsonl', 'as caption 4')

n = 14
bt = BETS['r48-s1']
ser1 = [{"name": "Frondium nisimocrax, small children", "points": [[t, c] for t, c in zip(bt['times'], bt['counts']['Frondium nisimocrax'])], "highlight": True},
        {"name": "Frondium marufubens, big children", "points": [[t, c] for t, c in zip(bt['times'], bt['counts']['Frondium marufubens'])]}]
caps, end = lay([
    "Tank 1: the small children won",
    "Marufubens peaked at 1,441 bodies, then fell to 2.",
    "Nisimocrax ended with 8,461 of the tank's 8,618 bodies.",
    "It had already led at 2,500 s, by 773 to 134.",
])
scene(n=n, act="Two leaves, two bets", station="Card", description="a title card: a slow drift through the crowd, under a full chart",
      run="r48-s1", second=30000, subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Tank 1's two leaf lines, alive", "place": "full",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "alive"}, "series": ser1},
      why="The bets compared over the whole run in tank 1.",
      sources=["lineage event sweep every 500 s, clades 43 and 39 (the guide's rule)",
               "peaks: 1,441 at 11,460 s; nisimocrax 8,480 at 29,955 s",
               "end: nisimocrax 8,461, marufubens 2, stats alive 8,618"])
check(n, 'caption 2', 'peaked at 1,441, fell to 2', '1,441 at 11,460 s; 2 at 30,000 s', S1 + '/lineage.jsonl', 'exact event sweep (the first story said 1,439 at 11,470 s from the guide\'s 10 s positions)')
check(n, 'caption 3', '8,461 of 8,618', '8,461; 8,618', S1 + '/lineage.jsonl; stats.jsonl', 'clade 43 at 30000; stats alive at 30000')
check(n, 'caption 4', 'led at 2,500 s by 773 to 134', '773; 134', S1 + '/lineage.jsonl', 'clades 43 and 39 at 2500')
for s in ser1:
    check(n, 'chart', s['name'], '%d points' % len(s['points']), S1 + '/lineage.jsonl', 'alive every 500 s: ' + json.dumps([p[1] for p in s['points']]))

n = 15
bt3 = BETS['r48-s3']
ser3 = [{"name": "Thallus legatribens, big children", "points": [[t, c] for t, c in zip(bt3['times'], bt3['counts']['Thallus legatribens'])], "highlight": True},
        {"name": "Thallus migugis, small children", "points": [[t, c] for t, c in zip(bt3['times'], bt3['counts']['Thallus migugis'])]}]
caps, end = lay([
    "Tank 3 ran the same contest, and big children won.",
    "Thallus legatribens: children at 59% of adult, 6,165 at the end.",
    "Thallus migugis: children at 9%, 2,340 at the end.",
    "I don't know why. Here too the winner led early, 280 to 30.",
])
scene(n=n, act="Two leaves, two bets", station="Time", run="r48-s3", **{"from": 3000, "to": 30000},
      subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Tank 3's two leaf lines, alive", "place": "corner",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "alive"}, "series": ser3},
      why="The same two bets with the opposite result, over a jump from 3,000 to 30,000 s in tank 3.",
      sources=["snapshot 30,000: legatribens median investment 0.681 brood 1 (n 6,165); migugis 0.111 brood 1 (n 2,340)",
               "birth sizes 20,000 to 30,000 s: 0.586 (24,106 births) and 0.088 (5,255)",
               "280 and 30 at 2,500 s: lineage clade walk"])
check(n, 'caption 2', 'children at 59% of adult', '0.5862', S3 + '/lineage.jsonl', "median 'bf', clade 63, births 20,000 to 30,000 s")
check(n, 'caption 2', '6,165 at the end', '6,165', S3 + '/lineage.jsonl', 'clade 63 at 30000')
check(n, 'caption 3', 'children at 9%', '0.0885', S3 + '/lineage.jsonl', "median 'bf', clade 3, births 20,000 to 30,000 s")
check(n, 'caption 3', '2,340 at the end', '2,340', S3 + '/lineage.jsonl', 'clade 3 at 30000')
check(n, 'caption 4', "I don't know why; the winner led early, 280 to 30", '280; 30 at 2,500 s', S3 + '/lineage.jsonl', 'clades 63 and 3 at 2500')
for s in ser3:
    check(n, 'chart', s['name'], '%d points' % len(s['points']), S3 + '/lineage.jsonl', 'alive every 500 s: ' + json.dumps([p[1] for p in s['points']]))

# ================================================================ chapter 6: a leaf keeps a stomach
n = 16
caps, end = lay([
    "Tank 1, 23,300 s. A nisimocrax leaf has a child.",
    "The child, body 48048, carries a bud: a speck of stomach.",
    "Rule one: a new kind of part arrives only as a small bud.",
])
scene(n=n, act="A leaf keeps a stomach", chapter="A leaf keeps a stomach", station="Birth", run="r48-s1",
      second=23300.5, subject="Vorafrons vabecrepis (guide clade 958), from Frondium nisimocrax; parent body 44820, child body 48048",
      seconds=18, captions=caps,
      why="The turn of the story: the birth that founds the one stomach line that lasts. A cousin will not reproduce this exact bud; the captions describe the recorded birth.",
      sources=["lineage: 48048 born 23,300.5 s to 44820 (clade 43, Frondium nisimocrax), bud absorptive, budx 1, flags (1,0,1)",
               "D119: a cell type arrives only as a bud, a small welded part at the born-small size"])
check(n, 'caption 1', 'tank 1, 23,300 s; a nisimocrax leaf', '23,300.5 s; parent 44820 in clade 43', S1 + '/lineage.jsonl', 'birth row 48048; parent born 22,486.5, died 24,038.5')
check(n, 'caption 2', 'body 48048 carries a bud of stomach', "bud 'absorptive', budx 1", S1 + '/lineage.jsonl', 'birth row 48048')
check(n, 'caption 3', 'rule one', 'D119', 'DECISIONS.md D119; Mutator (no ChangeCellType); Developer (welded bud)', 'a bud is a copy of a node at the born-small size with another cell type')

n = 17
row = LATE['row27500']
food_share = row['foodW'] / (row['foodW'] + row['lightW'])
caps, end = lay([
    "Body 48048, 4,200 s old: over twice the average age.",
    "It lives at the surface, and has had six children.",
    "Its stomach brings in 0.13% of its income. Light brings the rest.",
    "Like body 201 at the start: a leaf with a speck of stomach.",
])
scene(n=n, act="A leaf keeps a stomach", station="Portrait", run="r48-s1", second=27500, flexible=False,
      subject="body 48048", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "bars", "title": "Body 48048's income, %", "place": "corner",
             "at": caps[2]['at'], "until": round(end + 0.5, 1),
             "bars": [{"label": "light", "value": round(100 * (1 - food_share), 2), "highlight": True},
                      {"label": "snow", "value": round(100 * food_share, 2)}]},
      why="The founder of the lasting stomach line, at a checkpoint, read from its own row: it lives on light. The callback to body 201 closes the loop the opening started.",
      sources=["absorptive.jsonl id 48048 t 27500: age 4199.5, y -0.19, children 6, lightW 0.1396, foodW 0.000184",
               "stats meanAge 1,893.8 s at 27,500"])
check(n, 'caption 1', '4,200 s old', '4,199.5 s', S1 + '/absorptive.jsonl', 'row id 48048 t 27500')
check(n, 'caption 1', 'over twice the average age', '4,199.5 / 1,893.8 = 2.22', S1 + '/stats.jsonl', 'meanAge at 27500')
check(n, 'caption 2', 'at the surface; six children', 'y -0.186 m; children 6', S1 + '/absorptive.jsonl', 'row id 48048 t 27500')
check(n, 'caption 3', '0.13% of its income', '%.4f%%' % (100 * food_share), S1 + '/absorptive.jsonl', 'foodW 0.000184 / (foodW + lightW 0.13961); over its sampled life 0.130%')
check(n, 'caption 4', 'like body 201', 'both flags (1,0,1), both budded absorptive from a leaf, both 2 parts', S1 + '/lineage.jsonl; absorptive.jsonl', 'birth rows 201 and 48048; parts 2 in both rows')
check(n, 'chart', 'light', '%.2f%%' % (100 * (1 - food_share)), S1 + '/absorptive.jsonl', 'lightW / (lightW + foodW) at 27500')
check(n, 'chart', 'snow', '%.2f%%' % (100 * food_share), S1 + '/absorptive.jsonl', 'foodW / (lightW + foodW) at 27500')

n = 18
caps, end = lay([
    "Its line, Vorafrons vabecrepis, ended with 29 alive.",
    "No tank ended with a larger line carrying a stomach.",
    "Nine more descendants had dropped the stomach.",
    "Three other stomach buds on nisimocrax leaves left ten or more too.",
    "That second hope held here: a budded stomach founded a line.",
])
scene(n=n, act="A leaf keeps a stomach", station="Colony", run="r48-s1", second=30000,
      subject="Vorafrons vabecrepis (guide clade 958), founder body 48048", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Vorafrons vabecrepis, alive", "place": "corner",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "alive"},
             "series": [{"name": "Vorafrons vabecrepis", "points": X['line_48048'], "highlight": True}]},
      why="The turn's payoff: the prediction M3 holds in tank 1.",
      sources=["29: lineage clade of 48048 at 30,000 s (guide alive_at_end 29)",
               "largest: the stomach-carrying clades alive at each end, s1 29 / 19 / 10, s2 6, s3 3",
               "38 descendants alive: 29 with flags (1,0,1) and 9 with (0,0,1)",
               "M3: logbook/0119 (ten or more living in a budded stomach's line at 30,000 s in 1 of 3)"])
check(n, 'caption 1', '29 alive', '29', S1 + '/lineage.jsonl', 'clade 48048 at 30000')
check(n, 'caption 2', 'no larger stomach-carrying line in any tank', 's1 29, 19, 10; s2 6; s3 3', 'runs/r48-s*/lineage.jsonl', 'clades of living abs=1 bodies at each run\'s last sample, largest first')
check(n, 'caption 3', 'nine dropped the stomach', '9', S1 + '/lineage.jsonl', 'living descendants of 48048 with flags (0,0,1)')
check(n, 'caption 4', 'three other stomach buds on nisimocrax leaves left ten or more', 'living descendants at 30,000 s: 52803 31, 61200 10, 59978 10', S1 + '/lineage.jsonl', 'birth rows with bud absorptive whose parent is in clade 43 (Frondium nisimocrax), born 24,487, 26,522.5 and 26,217 s; none descends from another or from 48048')
check(n, 'caption 5', 'that second hope held here (a budded stomach founded a line)', 'M3: 38 descendants, 29 in the line', 'logbook/0119 M3', 'held in seed 1 only (s2 best 9, s3 best 6)')
check(n, 'chart', 'Vorafrons vabecrepis alive', json.dumps(X['line_48048']), S1 + '/lineage.jsonl', 'clade 48048 every 250 s from 23,000 s; peak 40 at 28,036.5 s')

# ================================================================ chapter 7: what the round found
n = 19
pser = []
for tank, arm in (('tank 1', 'r48-s1'), ('tank 2', 'r48-s2'), ('tank 3', 'r48-s3')):
    pser.append({"name": tank, "points": POP[arm]['points'], "highlight": tank == 'tank 1'})
caps, end = lay([
    "Tanks 1 and 3 each ended with about 8,600 bodies alive.",
    "Tank 2 stopped at 13,700 s on a software error.",
    "We have not found its cause yet.",
])
scene(n=n, act="What the round found", chapter="What the round found", station="Arrival", run="r48-s1", second=30000,
      subject="world", seconds=round(end + 0.5, 1), captions=caps,
      chart={"kind": "line", "title": "Bodies alive in each tank", "place": "corner",
             "at": caps[0]['at'], "until": round(end + 0.5, 1),
             "x": {"label": "time in the run, s"}, "y": {"label": "alive"}, "series": pser,
             "marks": [{"x": 3000, "label": "newcomers begin"}, {"x": 13700, "label": "tank 2 stops"}]},
      why="The whole round in one chart, over the end of tank 1.",
      sources=["stats.jsonl alive every 250 s, all three runs (seed 2 to its last row, 13,690 s)",
               "ends: 8,618 (s1), 8,589 (s3); s2 4,583 at 13,690",
               "13,700 s: r48-s2 run.json status error, simulatedSeconds 13700, ending 'Field densityHere is -Infinity'"])
check(n, 'caption 1', 'tanks 1 and 3 each ended with about 8,600 bodies alive', '8,618 and 8,589', 'runs/r48-s1, r48-s3 stats.jsonl', 'alive at 30000')
check(n, 'caption 2', 'stopped at 13,700 s on a software error', '13,700 s; status error', S2 + '/run.json', "ending: ArgumentException: Field 'densityHere' is -Infinity (absorptive log writer)")
check(n, 'caption 3', 'cause not found yet', 'open', 'HANDOFF.md (2026-09-25); scratch/film/r48-s2-probe.log', 'a probe of seed 2 was running at 10:22 on 2026-09-25; update this caption if it finds the cause')
for s in pser:
    check(n, 'chart', s['name'], '%d points' % len(s['points']), 'stats.jsonl', 'alive every 250 s: ' + json.dumps(s['points']))
check(n, 'chart mark', 'newcomers begin', '3,000 s', S1 + '/config.json', 'population floor closes (3000 s) and the trickle begins')
check(n, 'chart mark', 'tank 2 stops', '13,700 s', S2 + '/run.json', 'simulatedSeconds 13700')

n = 20
caps, end = lay([
    "Tank 1 at the end. All but three of its bodies carry a leaf.",
    "The eaters landed well, lived longer and bred, then died out.",
    "Nearly every stomach left at the end rides on a leaf.",
    "The round got its stomach on a leaf. It did not get its eaters.",
])
scene(n=n, act="What the round found", station="Descent", run="r48-s1", second=30000, subject="world",
      seconds=round(end + 1.5, 1), captions=caps,
      why="The bittersweet verdict, over the crowd of leaves that won.",
      sources=["stats.jsonl s1 t 30000: alive 8,618, photosynthetic 8,615",
               "bodies with a stomach at the ends: s1 121 (at least 118 with a leaf), s2 35 (at least 30), s3 29 (at least 25): alive - photosynthetic bounds the leafless"])
check(n, 'caption 1', 'all but three carry a leaf', '8,615 of 8,618', S1 + '/stats.jsonl', 'photosynthetic and alive at 30000')
check(n, 'caption 2', 'landed well, lived longer, bred, died out', 'chapters 3 and 4', 'as scenes 8, 9, 12', 'summary of checked rows')
check(n, 'caption 3', 'nearly every stomach rides on a leaf', 's1 >= 118/121, s2 >= 30/35, s3 >= 25/29', 'runs/r48-s*/stats.jsonl', 'absorptive minus (alive - photosynthetic) at each end')

# ---------------------------------------------------------------- validate and write
problems = []
total = 0.0
chapters = 0
for sc in scenes:
    secs = sc['seconds']
    total += secs + (8.0 if sc.get('chapter') else 0.0)
    chapters += 1 if sc.get('chapter') else 0
    if sc['station'] == 'Card' and sc.get('chapter'):
        problems.append('scene %d: a chapter on a card' % sc['n'])
    for c in sc['captions']:
        out, bad = printable(c['text'])
        if bad:
            problems.append('scene %d: caption outside the font %r: %s' % (sc['n'], bad, c['text']))
        h = c.get('seconds', 4.0)
        if len(out) / h > 14.5:
            problems.append('scene %d: caption reads at %.1f chars/s: %s' % (sc['n'], len(out) / h, c['text']))
        if c['at'] + h > secs + 1e-6:
            problems.append('scene %d: caption ends at %.1f after the scene\'s %.1f s' % (sc['n'], c['at'] + h, secs))
    ch = sc.get('chart')
    if ch:
        for key in ('title',):
            out, bad = printable(ch[key])
            if bad:
                problems.append('scene %d: chart title outside the font %r' % (sc['n'], bad))
        for lab in [b['label'] for b in ch.get('bars', [])] + [s['name'] for s in ch.get('series', [])] + \
                   [m['label'] for m in ch.get('marks', [])] + [ch.get('x', {}).get('label', ''), ch.get('y', {}).get('label', '')]:
            out, bad = printable(lab)
            if bad:
                problems.append('scene %d: chart label outside the font %r: %s' % (sc['n'], bad, lab))
        if ch.get('until', secs) > secs + 1e-6 or ch['at'] >= secs:
            problems.append('scene %d: chart outside the scene' % sc['n'])
        if ch['kind'] == 'account' and sc['station'] not in ('Portrait', 'Birth'):
            problems.append('scene %d: an account chart on a %s' % (sc['n'], sc['station']))
        # A full chart may sit on any station since 2026-09-25, the world moving under it at two
        # thirds of its light; on a portrait or a birth its card would cover the body the scene is about.
        if ch['place'] == 'full' and sc['station'] in ('Portrait', 'Birth'):
            problems.append('scene %d: a full chart on a %s covers its body' % (sc['n'], sc['station']))

TITLE_SECONDS = 5.0
story = {
    "run": "r48 (r48-s1, r48-s2, r48-s3)",
    "runs": RUNS,
    "title": "Round 48: the eaters faded, and a leaf kept a stomach",
    "version": "2 (2026-09-25): the glossary rule, an opening chapter on how the world works, a story arc (bittersweet), charts; the cards drift through the moving world under their charts, and the captions are subtitles set at the join",
    "arc": "Bittersweet. We changed four rules so that eaters of snow could found a lasting line; stored eaters landed well, lived minutes, bred and grew lines of up to 45, and every one of those lines died out. What lasted was a leaf that budded a speck of stomach and founded a line of 29, the largest stomach line in any tank, living almost wholly on light.",
    "final": "all three seeds read at their ends: r48-s1 and r48-s3 ended at 30,000 s (budget); r48-s2 stopped on an error at 13,700 s, last good row 13,690 s",
    "screen_seconds": round(total, 1),
    "chapter_cards": "each scene carrying 'chapter' plays an 8 s chapter card first; screen_seconds includes them and not the 5 s title",
    "scenes": scenes,
}
json.dump(story, open(os.path.join(HERE, 'story.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)

with open(os.path.join(HERE, 'checks.tsv'), 'w', encoding='utf-8', newline='') as f:
    f.write('scene\titem\tshown\texact\tsource\thow_read\n')
    for r in CHECKS:
        f.write('\t'.join(x.replace('\t', ' ').replace('\n', ' ') for x in r) + '\n')

print('scenes', len(scenes), 'chapters', chapters, 'screen seconds', round(total, 1), '+ title', TITLE_SECONDS,
      '=', round(total + TITLE_SECONDS, 1), 's =', '%d min %02d s' % divmod(round(total + TITLE_SECONDS), 60))
for sc in scenes:
    print('%2d %-7s %-6s %-9s %6s s %s%s' % (sc['n'], sc['run'], sc['station'], sc.get('second', '%s-%s' % (sc.get('from'), sc.get('to'))),
                                           sc['seconds'], ('[' + sc['chapter'] + '] ') if sc.get('chapter') else '',
                                           ('chart ' + sc['chart']['kind'] + '/' + sc['chart']['place']) if sc.get('chart') else ''))
print('checks rows', len(CHECKS))
print('PROBLEMS:' if problems else 'no problems')
for p in problems:
    print('  ', p)
