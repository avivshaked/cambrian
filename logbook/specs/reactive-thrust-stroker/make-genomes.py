"""Two hand-built stroking genomes, written in GenomeJson's own field order.

Round 42's evolved bodies all settle on a constant drive, so none of them strokes.
These two do: every driven link carries one OscillateWave neuron, which Brain.cs
evaluates as sin(2*pi*f*t + phase) * amplitude + bias from simulation time alone
(no inputs, no sensors), and neuron d of a part drives degree of freedom d of its
joint.

Geometry: elongated boxes stacked end to end along local Y, each child's -Y face
anchored to its parent's +Y face. A Hinge's joint frame is Euler(0, 90, 0), so its
one degree of freedom turns about the child's local Z -- across the stack, which is
a bend rather than a twist. Parts touch at a face and never interpenetrate, and only
adjacent pairs ever meet, so self-collision cannot enter the comparison.
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))

# ---- dials, in one place so a retry is one edit ------------------------------
FREQ = float(os.environ.get("STROKER_FREQ", 0.5))    # Hz -> 2.0 s period, in the 1-3 s window
POWER = float(os.environ.get("STROKER_POWER", 1.1))  # N.m peak; round 42's links carry 5-20
LIMIT = float(os.environ.get("STROKER_LIMIT", 0.6))  # rad, +-
# pi/2, so the drive is a cosine. A joint at this size is drag-dominated rather than
# inertia-dominated, so its angle is close to the integral of the drive torque: a sine
# started at phase 0 integrates to (1 - cos)/w, which never goes negative and left the
# stroke sitting on its positive stop. A cosine integrates to sin/w, centred on zero.
PHASE0 = float(os.environ.get("STROKER_PHASE", 1.5707963))
PHASE_LAG = 1.5707963  # rad, pi/2 -> a quarter period between the two hinges
# The lag turns the distal joint's cosine back into a sine, so its own integral is
# offset by 1/w again -- the price of a travelling wave. It is bought back by giving
# that joint less torque, which shrinks the offset and the swing together and keeps
# the stroke clear of the stop.
POWER2 = float(os.environ.get("STROKER_POWER2", 0.7))
ROOT_H = (0.06, 0.12, 0.06)
LINK_H = (0.05, 0.15, 0.05)


def f3(x, y, z):
    return {"x": x, "y": y, "z": z}


def edge(child):
    """Parent's +Y face to the child's -Y face, no turn, no reflection."""
    return {
        "child": child,
        "terminalOnly": False,
        "parentAnchor": f3(0.0, 1.0, 0.0),
        "childAnchor": f3(0.0, -1.0, 0.0),
        "scale": f3(1.0, 1.0, 1.0),
        "orientation": {"x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0},
        "reflect": {"x": False, "y": False, "z": False},
    }


def oscillator(phase):
    return {
        "op": "OscillateWave",
        "frequency": FREQ,
        "phase": phase,
        "amplitude": 1.0,
        "bias": 0.0,
        "inputs": [],
    }


def node(cell, joint, power, half, limits, edges, neurons, recursive=1):
    return {
        "cell": cell,
        "shape": "box",
        "joint": joint,
        "power": power,
        "lift": 0.0,
        "recursiveLimit": recursive,
        "dimensions": f3(*half),
        "jointLimits": limits,
        "edges": edges,
        "neurons": neurons,
    }


def genome(oid, nodes):
    return {
        "id": oid,
        "format": 6,
        "root": 0,
        "adultScale": 1.0,
        "reproduction": {"brood": 1, "investment": 0.5, "margin": 0.0},
        "nodes": nodes,
        "globalBrain": [],
    }


stop = [{"min": -LIMIT, "max": LIMIT}]

# (a) two boxes, one hinge, one oscillator.
a = genome(9001, [
    node("structural", "Fixed", 0.0, ROOT_H, [], [edge(1)], []),
    node("link", "Hinge", POWER, LINK_H, stop, [], [oscillator(PHASE0)]),
])

# (b) three parts in a chain, two hinges a quarter period apart.
b = genome(9002, [
    node("structural", "Fixed", 0.0, ROOT_H, [], [edge(1)], []),
    node("link", "Hinge", POWER, LINK_H, stop, [edge(2)], [oscillator(PHASE0)]),
    node("link", "Hinge", POWER2, LINK_H, stop, [], [oscillator(PHASE0 + PHASE_LAG)]),
])

out = os.environ.get("STROKER_OUT") or os.path.join(HERE, "snapshot.jsonl")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8", newline="\n") as handle:
    for row in (a, b):
        handle.write(json.dumps(row, separators=(",", ":")) + "\n")

print("wrote", out)
