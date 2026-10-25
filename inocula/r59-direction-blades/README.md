# The blade founders

Ten genomes, one for each of round 59's planted grazers in `inocula/r59-direction-tops/`. Each is its parent with the
limb's capsule replaced by a flat box of the same volume (D236). The joint, its axis and limits, the drive genes, the
brain, the mouth, the trunk and the edge that hangs the limb are the parent's, character for character.

The parent's limb is a capsule 0.52 m long and 0.16 m thick, on a hinge whose axis stands vertical, so it swings in the
level plane. Under D229's fin law a capsule takes the same added mass whichever way it moves, and a box takes it in
proportion to the square of its width across the swing. The blade is the same limb made flat across its stroke.

## The blade

The two shapes compare as follows. All ten parents carry the same capsule, so all ten blades carry the same box.

| | half-extents (m), x by y by z | volume (m³) |
|---|---|---|
| the capsule | 0.08 by 0.26 by 0.08 | 0.00938289 |
| the blade | 0.017351303 by 0.26 by 0.25998074 | 0.00938289 |

The two volumes agree to 9e-12 relative, computed in double precision on the stored float values. Core computes each in
single precision and reads 0.00938288961 for both.

The long axis, y, keeps the capsule's half-length of 0.26 m, so the tip stands as far from the joint as before. The
limb is attached by the middle of its near end. That is the same point on a box as on a capsule, so the limb hangs from
the same face of the trunk. The width is the z extent, along the hinge axis. The thickness is the x extent, along the
swing.

Round 58's link limit of 0.26 m bounds every half-extent of a link, and D236 puts the blade inside it. So the length and
the width are both held to 0.26 m, and the volume sets the thickness. That makes the blade the flattest box of the
capsule's volume inside the limit: the width is 14.98 times the thickness, about 15 to 1. The fin bench's paddle, 0.2 by
0.01 by 0.2 m (`logbook/specs/d228-d229-build/README.md`), is 20 to 1, but at this volume that would need a width of
0.30 m. The width is a few float steps under 0.26 m, 0.25998074, because those steps are what let the volume match. The
thickness is above the development floor of 0.01 m, so nothing is raised or pruned.

By the fin law's formula the blade takes about 110 kg of added mass for a stroke, against about 8.3 kg for the capsule.
That is my arithmetic with the density of water at 1000 kg per cubic metre, and the test checks it against the code.

## Which way is the width

I read the orientation from the code, and not from the brief. The edge turns the limb a quarter turn about the trunk's
x axis, so the limb's local z lies along the trunk's vertical. The hinge's axis is the x of the joint frame, and that
frame is a quarter turn about y (`Creature.FrameOf`), which sends x to minus z of the limb's own frame. The hinge axis is
therefore the limb's z, and vertical.

The fin law takes a box's width as its extent along the limb's axis crossed with the motion. The motion is the hinge axis
crossed with the limb's axis. The width is therefore the extent along the hinge axis, which is the limb's z.

## How they were made

`scripts/reads/r59-blades.py` makes them and can be run again. It reads each parent as its committed bytes, with LF line
ends, and parses it keeping every number as text. Before it changes anything it writes all ten parents back through its
serialiser, and it refuses unless each comes back byte for byte. That proves the serialiser writes as `GenomeJson` does.
Then it changes the limb's shape and dimensions and writes the file as one line and a newline. It diffs the result
against the parent and refuses unless only those two fields of that node differ.

The volumes are Core's `CapsuleShape.Volume` and `BoxShape.Volume`, transcribed in the script with their line numbers. A
half-extent is a single-precision float, and rounding three of them one at a time cannot reach 1e-9 of a volume. The
script steps the width down from 0.26 m, never above it, and takes the nearest floats of the thickness at each step, for
the triple whose exact product is closest.

`origins.tsv` gives each file's SHA-256, its parent's file and SHA-256, and the capsule's and the box's dimensions and
volumes. A file's SHA-256 is over its bytes with LF line ends, as committed.

`pool.tsv` is a weighted list the farm's trickle reads, as `EVOSIM_TRICKLE_POOL=@inocula/r59-direction-blades/pool.tsv`.
It follows the tops' format and order and holds the ten blades at equal weight. Read that way it pins a pool of 10 genomes
with the hash `70ee22dab352b074715b67de957999b79e0def753ef238610cb89957b646f096`. The `ownEaten` column is empty,
because a blade has not swum.

`pool-mixed.tsv` holds the twenty, the ten tops and then the ten blades, at a weight of 0.05 each. It pins a pool of 20
genomes with the hash `0a10451e224dba144dcf5375ce18f03824e39ffa9c60ac1bccd857e976f9762f`. The tops are named by their
path in `../r59-direction-tops/`.

`lineage.json` gives the founder lineage of each line of the nursery pool that `scripts/reads/curriculum-pool.py` makes from
`pool-mixed.tsv`, keyed by line from 0. A blade is of its top's lineage, so the twenty lines make ten lineages: a blade
carries its top's brain, so the two are no independent draws. The same script writes it, and `founders-batch.py --lineage`
copies it into a batch for `scripts/reads/nursery-travel-read.py`.

I computed both hashes with a Python copy of the trickle's hashing, which gives the tops' own hash (`ec0baadf…`) for the
tops' list. `BladeFoundersTests` checks both against Core's reader.

These genomes plant nothing. A round's pre-registration sets which list the trickle reads, the share it takes, and what
else it plants.
