"""Read a nursery run's profile.jsonl (EVOSIM_NURSERY_PROFILE=1): where a pool of tanks spent its wall.

Usage: python scripts/reads/nursery-profile-read.py <run folder> [<run folder> ...] [--seconds 600]

Per pool (a generation's swim or an exam control's): the wall, the threads, the share of the
threads' time spent in tank jobs, the tail (from the first moment a thread has no job left to the
pool's end, and the thread-seconds idle in it), and the episode split summed over the pool's jobs:
setup, world, physics by phase, the bodies' phase by the water at their links, senses, brain and
the rest, and the harness.
Also A5's ceiling: the simulated seconds a tank swam after its last body died, as a share of all.
"""
import json
import sys


def read(folder, episode_seconds):
    rows = [json.loads(line) for line in open(f"{folder}/profile.jsonl", encoding="utf-8") if line.strip()]
    print(f"== {folder}")
    for r in rows:
        jobs, wall, threads = r["jobs"], r["wallMs"], r["threads"]
        work = sum(j["endMs"] - j["startMs"] for j in jobs)
        used = work / (threads * wall) if wall > 0 else float("nan")
        last_start = max(j["startMs"] for j in jobs)
        ends = sorted(j["endMs"] for j in jobs)
        # The tail: once the last job has started, every thread that finishes has nothing to take.
        idle = sum(max(0.0, wall - e) for e in ends[-threads:]) if len(ends) >= threads else threads * wall - work
        per = {k: sum(j[k] for j in jobs) for k in
               ("setupMs", "worldMs", "physicsMs", "harnessMs", "gridMs", "pinMs", "bodiesMs", "commitMs",
                "afterMs", "waterMs", "senseMs", "brainMs", "restMs")}
        steps = sum(j["bodySteps"] for j in jobs)
        loop = per["worldMs"] + per["physicsMs"] + per["harnessMs"]
        total = per["setupMs"] + loop
        dead = [j for j in jobs if j["allDeadAt"] is not None]
        wasted = sum(episode_seconds - j["allDeadAt"] for j in dead)
        print(f"pool {r['pool']} gen {r['gen']} salt {r['salt']}: {len(jobs)} jobs, {threads} threads, wall {wall/1000:.1f} s, "
              f"cpu {r['cpuMs']/1000:.1f} s; threads busy {used:.1%}; last job started at {last_start/1000:.1f} s, "
              f"tail {(wall - last_start)/1000:.1f} s, idle in the last wave {idle/1000:.1f} thread-s")
        if total > 0:
            def s(k):
                return f"{per[k]/1000:.1f} s ({per[k]/total:.1%})"
            print(f"  jobs' time {total/1000:.1f} s: setup {s('setupMs')}, world {s('worldMs')}, physics {s('physicsMs')}, harness {s('harnessMs')}")
            print(f"  physics: grid {s('gridMs')}, pin {s('pinMs')}, bodies {s('bodiesMs')}, commit {s('commitMs')}, after {s('afterMs')}")
            print(f"  bodies: water {s('waterMs')}, senses {s('senseMs')}, brain {s('brainMs')}, rest {s('restMs')}")
            if steps:
                us = lambda k: per[k] * 1000.0 / steps
                print(f"  per body-step: senses {us('senseMs'):.3f} us, brain {us('brainMs'):.3f} us, rest {us('restMs'):.3f} us, "
                      f"water {us('waterMs'):.3f} us, grid {us('gridMs'):.3f} us ({steps} body-steps)")
        print(f"  A5: {len(dead)} of {len(jobs)} tanks had every body dead before the end; "
              f"{wasted:.0f} of {len(jobs)*episode_seconds:.0f} simulated s swum after ({wasted/(len(jobs)*episode_seconds):.2%})")


def main(argv):
    seconds = 600.0
    if "--seconds" in argv:
        i = argv.index("--seconds")
        seconds = float(argv[i + 1])
        argv = argv[:i] + argv[i + 2:]
    if len(argv) < 2:
        print(__doc__)
        return 2
    for folder in argv[1:]:
        read(folder, seconds)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
