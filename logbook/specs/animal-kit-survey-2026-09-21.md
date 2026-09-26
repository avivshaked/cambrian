# Repertoire survey, 2026-09-21 (subagent, read-only; verify a line before citing it)

Shapes: box, sphere, capsule (`src/Evosim.Core/Shapes/StandardShapes.cs`, registry order is RNG- and
hash-significant, `PartShapeRegistry.cs:51`). ShapeChance 0.02. MaxParts 16, MaxDepth 8,
MinPartVolume 1e-4, MinPartHalfExtent 0.01. Joints: Fixed, Hinge, Twist, HingeTwist, TwistHinge,
Universal, Spherical.

Cell types (`Cells/StandardCellTypes.cs`): structural, link (only one that may carry a joint),
neural, photosynthetic, absorptive, consumer, buoyancy. CellTypeChance 0.001
(`EVOSIM_CELLTYPE_MUTATION`).

**Consumer cell has a live-bite branch that nothing feeds**: `ConsumerCell.Acquire`
(`StandardCellTypes.cs:713-728`) reads `context.Contact` with yield tiers carrion 0.8, grazing 0.5,
predation 0.2; `Metabolism.cs` never sets `CellContext.Contact`. So predation is half built in Core.

Sensors (`Genome/NeuronInput.cs:52-190`, `Brain/SensorChannels.cs:52-61`,
`unity/Assets/Evosim/Sim/CreatureSensors.cs`): answered today JointAngle, JointAngularVelocity,
OrientationUp, Depth, Chemical, Energy, Flow. Declared and reading 0: Contact, Photo, Damage.
No channel reports another creature. **D020 (`DECISIONS.md:596-606`) rejected any bearing sensor and
image-forming vision**: direction is meant to come from the body comparing scalar readings at
several parts. A neighbour sense inside D020 is a scalar "scent of bodies" field, or Contact/Damage.

Effectors: joint torque only. No bite action (eating is passive), no emission, no signalling, no
brain control of lift (Lift is a genome constant), no timing of reproduction.

Deaths: Starved and Diverged only (`World.cs:2238, 2431-2472`).

Planned, not built: Contact/Damage/Photo sensors (DESIGN §4.4), predation (§5A.3,
`fable-propose-predation.md`), colour (§5A.5), sexual reproduction (§5A.6), attack/defence
"deliberately unspecified" (§5A.3, D017).

Cost of a genome field: FormatVersion bump (every stored genome refused), mutator wiring,
hash contributions (`CellType.cs:240-258`, `PartShape.cs:126`), the picture-only readers. A new
shape or cell type that adds no MorphNode field bumps no format but moves the registry order and
the config hash.
