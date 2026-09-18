# Cloud CPU for the arms: options, prices, and what they would cost the record

**2026-09-18**  ·  a survey for the owner, asked for that morning ("prices and options for
cloud CPU, to run simulations outside our machine, pros and cons and prices, an estimate of
overall costs"). Four research agents read provider pages and pricing feeds; the agent
synthesised. Every price below is marked **read** (from the provider's own page or feed on
2026-09-18) or **estimated** (arithmetic on read numbers, or a benchmark ratio). Nothing
was bought, nothing was sent anywhere, and no account was opened.

## The workload, read from round 39

| fact | value | source |
|---|---|---|
| one seed | 30,000 simulated s, 3 million physics steps | `run.json` |
| wall clock, three arms on the desktop | 15.7 h a seed (0.53 of real time) | `r39-s1`'s manifest |
| cores a seed uses | about one, single-threaded by design (D078, `physicsJobWorkers 0`) | `run-arm.ps1` |
| memory a seed uses | about 1.5 GB | task manager, round 39 |
| a run directory | 160 MB (122 MB of snapshots) | `du` on `r39-s1` |
| where the wall goes at founding | world 85%, harness 11%, physics 4% | the timing split (CLAUDE.md) |

Three things follow before any price. The work is one core per seed, so a machine is
worth what its single core is worth, and core counts are worth nothing beyond the number
of seeds run at once. The output is small, so data transfer is free everywhere and never
enters the decision. And the desktop's three-arm rule (D095) already gives about four
seeds a day, so the desktop is not short of throughput at the campaign's pace of a round
every two or three days. What a second machine buys is latency (a five-seed round in one
batch instead of two), room for ten-seed rounds, and a desktop free for renders and the
owner's Editor.

## The licence is the deciding constraint

Read from Unity's own pages (Editor Software Terms, updated 30 June 2026; the licence
compliance page; the pricing page).

- One seat allows one instance at a time, on a primary and a secondary computer, for one
  person. The compliance page says "running Unity on more than one machine at the same
  time is not allowed" and "a separate license is required for build machines".
- The clause about the cloud binds third-party providers running the Editor for you. A
  rented box you run yourself is you, as the authorised user. That clause is not the
  problem.
- Build Server licences cover batch builds only ("the sole purpose of generating builds"),
  so an `-executeMethod` simulation is outside them. Unity Simulation, which was the
  product for this, was deprecated in December 2023 with no successor.
- Personal is free under $200,000 of total finances, has no serial for command-line
  activation, and Unity's pricing page says it is for gaming and entertainment
  applications. Pro is $2,310 a year a seat (read, after the January 2026 rise).

My reading, marked as inference from the terms and not a legal opinion: the concurrency
clause is the one that binds, and on its letter it already binds the desktop's three
batch-mode arms beside the owner's Editor. The terms count instances and the compliance
page counts machines, and Unity has not reconciled the two in writing. A second machine
removes the ambiguity the desktop benefits from. The cost of running N machines under Pro is N seats, $2,310 a year each, which is three
to seven times the compute below.
So the first step is not a purchase. It is one question to Unity sales. **May a Pro seat, or a Build Server seat, run the
Editor in batch mode on a rented server for a non-build simulation workload?** A yes on
Build Server makes ten machines one seat's price. A no
means each machine is a seat, and the idea is probably not worth it at this project's
size. I would not go past a one-month trial without that answer, and the owner's own
licence tier, which I do not know, decides which question to ask.

## What a rented core is worth against the desktop

Single-thread scores relative to the i9-13900K, from PassMark single-thread and from
per-instance measurements on live AWS instances (RunsOn, updated 2026-09-02). Geekbench
figures for cloud CPUs could not be read; the benchmark agent's note is that the truth
for a scalar C# loop sits between PassMark and Geekbench, nearer PassMark. Seed hours are
15.7 divided by the ratio, **estimated**.

| machine | CPU | ratio | seed hours |
|---|---|---|---|
| the desktop | i9-13900K | 1.00 | 15.7 (read) |
| desktop Ryzen bare metal | Ryzen 9 9950X | 1.03 | 15 |
| desktop Ryzen bare metal | Ryzen 9 7950X3D | 0.90 | 17 |
| AWS m8azn (Turin 5.0 GHz) | EPYC 9575F | 0.93 | 17 |
| AWS c8a | EPYC 9R45 | 0.83 | 19 |
| GCP C4D | EPYC 9B45 | 0.77 | 20 |
| AWS c8i, GCP C4 | Xeon 6 Granite Rapids | 0.69 | 23 |
| AWS c7i, Azure v6, GCP C3 | Sapphire or Emerald Rapids | 0.54 to 0.65 | 24 to 29 |
| Hetzner Cloud CCX | EPYC Genoa or Milan, masked | about 0.62 (estimated) | 25 |
| AWS c8g (Arm) | Graviton4 | 0.42, and no Arm Editor | excluded |

The line that matters: nothing a hyperscaler rents is as fast per core as the desktop,
and a seed there takes a day rather than sixteen hours. Only bare metal with a desktop
Ryzen matches it. That is also the cheapest option, which is unusual and is the finding.
A cloud seed needs its wall set from its own pace, not the desktop's, or it is censored
the way round 39's seeds 3 and 4 were (CLAUDE.md's gotcha).

## Prices, read on 2026-09-18

### Hyperscalers, on demand, US East, an hour of a two-vCPU instance

| instance | Linux | Windows | note |
|---|---|---|---|
| AWS c8i.large | $0.094 | $0.186 | 3.9 GHz all-core published; the best documented clock per dollar |
| AWS c7i.large | $0.089 | $0.181 | 3.2 GHz all-core |
| AWS c8a.large | $0.108 | $0.200 | Turin, "up to 4.5 GHz", not broken down |
| AWS z1d.large | $0.186 | $0.278 | 4.0 GHz all-core, twice the price, older core |
| Azure F2als_v6 | $0.121 | $0.213 | two physical cores, no SMT; the Intel Fsv6 in the brief does not exist |
| Azure F2s_v2 | $0.085 | $0.177 | the host CPU varies by region |
| GCP c4-highcpu-2 | $0.085 | $0.177 | Granite Rapids; Windows is $0.046 a vCPU on top |
| GCP c4d-highcpu-2 | $0.077 | $0.169 | Turin |

Windows doubles the hour on every one of them and buys nothing once the scripts are
ported. Spot is 60 to 70% off on Linux, but AWS's own advisor puts c7i.large above 20%
interruption a month in US East, and a seed cannot checkpoint, so a reclaimed instance is
a lost day. The safer spot bands (c7a, c8i at 5 to 10%) still make a scored seed a gamble;
spot is for a smoke, never for a pre-registered round. EU regions are 5 to 10% dearer
on demand and their spot is cheaper; GCP's EU prices could not be read.

### Bare metal with a desktop CPU, a month

| provider and plan | CPU | cores | RAM | a month | setup | source |
|---|---|---|---|---|---|---|
| OVH RISE-M | Ryzen 9 9900X | 12 | 64 GB | **$118** | one month, waived on a 12-month term | read, OVH US |
| OVH RISE-L | Ryzen 9 9950X | 16 | 128 GB ECC | **$177** | same | read, OVH US |
| OVH RISE-S | Ryzen 7 9700X | 8 | 64 GB | $77 | same | read |
| Hetzner AX102-1-LTD | Ryzen 9 7950X3D | 16 | 128 GB | $187 (€157) | €39 | read; limited stock |
| Hetzner AX102-1 | Ryzen 9 7950X3D | 16 | 128 GB | $302 | €129 | read; Hetzner raised prices 15 June 2026 |
| InterServer | Ryzen 9 9950X | 16 | 96 GB | from $253 | none | read |
| RackNerd | Ryzen 9 7950X3D | 16 | 128 GB | $379 | not stated | read |
| Scaleway Elastic Metal, hourly | EPYC 4245P (desktop Zen 4) | 6 | 32 GB | €0.25 an hour | none | read; the only hourly bare metal near the clock |

Hetzner's AX52, the plan the first draft priced, no longer exists; Hetzner's whole
catalogue rose 15 June 2026 and only the limited tier is still competitive. The Hetzner
server auction was excluded from the rise and is probably the cheapest 7950X3D on the
market, but its prices render only in a browser. OVH's Windows licence price is not
published anywhere; Hetzner's and Scaleway's are, about $29 a month for 8 cores and $57
for 16, which is the price of not porting to Linux.

## The cost of a seed, a round and a month

These are estimates. A seed is 15.7 h divided by the ratio; a month is 40 seeds (eight
rounds of five), which is about the campaign's pace. The licence is excluded and is the largest
line if it applies.

| option | seed | five-seed round | a month | round latency |
|---|---|---|---|---|
| AWS c8i.large, Linux, on demand | $2.14 | $10.70 | $86 | 23 h, five at once |
| AWS c8i.large, Windows | $4.24 | $21.20 | $170 | 23 h |
| GCP c4-highcpu-2, Linux | $1.94 | $9.70 | $78 | 23 h |
| Azure F2als_v6, Linux | $3.06 | $15.30 | $122 | 25 h |
| OVH RISE-M, 12-month term | $0.24 | $1.20 | **$118 flat** | 15 h, ten at once |
| OVH RISE-L, 12-month term | $0.28 | $1.40 | **$177 flat** | 15 h, thirteen at once |
| Hetzner AX102-1-LTD | $0.30 | $1.50 | $187 flat | 17 h, thirteen at once |
| Scaleway EM hourly, five seeds at once | $0.85 | $4.30 | $34 at this pace | 22 h; wins only under 35% duty |
| a Pro seat, if one machine needs one | | | $193 | |

A year on one OVH RISE-L is about $2,100 with no setup on the term. With one Pro seat on
top it is about $4,400. Against that, the desktop already runs the campaign's pace. The money buys the round's
latency and the ten-seed rounds; the ability to run at all it already has.

## What a cloud run costs the record

The campaign's replay guarantee (D078, the theatre's identity check) holds on this
machine. PhysX is not deterministic across CPUs, and the world step is Mono-compiled C#
whose code differs by CPU feature set, so a run recorded on a Xeon and replayed on the
13900K will part. Three things follow from that.

- **A cloud recording plays in the theatre as a cousin**, not a replay: the identity
  check fails and the provenance strip says so. The rule that no round is written up
  until it has been watched (CLAUDE.md, 0083) is then a rule about a cousin. That is the
  state 0069 and 0070 were written to escape.
- **A dedicated box replays with itself**, as the desktop does. That is a second reason to
  prefer bare metal over a VM family that may be backed by more than one CPU stepping.
- **The manifest does not carry the CPU.** `run.json`'s source block has the commit, the
  hashes and the worker path and nothing about the machine. A cloud run would look like a
  local one until someone tried to replay it. The
  CPU model, the core count and the OS go into the manifest before the first remote seed.

The workable split: screening in the cloud, scoring and pictures at home. Smokes, dt 0.02
screens, ledger-style A/Bs, reruns whose value is a number, and the second batch of a
ten-seed round go remote. The base round of a campaign, and anything that will be
watched, photographed and entered, stays on the desktop where it replays.

## The port

Eighteen PowerShell scripts, six of which touch `Start-Process`, `Get-CimInstance` or the
hard-coded Editor path. PowerShell 7 runs on Linux, and the Linux Editor supports Ubuntu to 24.04 with batch
mode as the documented headless path, so nothing structural blocks it. The work is the
launch wrapper in the arm script, the process and lock checks the monitors use, and paths. A day or two, plus a first replay check that will fail against
local recordings by design and should be agreed as such before it runs. Windows on a
rented box avoids the port and costs $29 to $57 a month where the price is published, and
an unknown amount at OVH.

## Recommendation

1. **Ask Unity the licence question before spending anything.** The answer changes the
   economics by more than any price here. The owner's licence tier decides which form of
   the question to ask.
2. **If the answer allows it, one OVH RISE-L (or RISE-M) on Linux, month to month for the
   first month.** The setup fee is one month's rent, and the 12-month term waives it once
   the box has earned it. That is $118 to $177 a month, a five-seed round in fifteen
   hours, a ten-seed round in the same, and the desktop free for renders. Hetzner's AX102-1-LTD is
   the alternative if OVH's stock is out.
3. **Never a scored seed on spot**, and never Windows in the cloud once the six scripts
   are ported.
4. **Before the first remote seed**: the CPU model in `run.json`, and the wall set from the
   box's own measured pace.
5. **Keep the base rounds at home.** The theatre's guarantee is worth more than a day of
   latency, and the record should say which machine every run came from.

If the licence answer is no, the cheapest legitimate route is a hyperscaler on demand
with one extra Pro seat, about $86 plus $193 a month for 40 seeds. At that price the
desktop's three arms are the better deal.

## Sources

Unity: [Editor Software Terms](https://unity.com/legal/editor-terms-of-service/software) ·
[licence compliance](https://unity.com/pages/license-compliance) · [pricing](https://unity.com/pricing) ·
[pricing updates](https://unity.com/products/pricing-updates) ·
[activations](https://support.unity.com/hc/en-us/articles/4402049280276) ·
[Build Server and the Editor UI](https://support.unity.com/hc/en-us/articles/4401984205204) ·
[Unity Simulation deprecation](https://docs.unity3d.com/Simulation/manual/index.html) ·
[headless mode](https://docs.unity3d.com/6000.5/Documentation/Manual/desktop-headless-mode.html) ·
[PhysX determinism](https://discussions.unity.com/t/determinism-physx/866457).
Benchmarks: [PassMark single thread](https://www.cpubenchmark.net/singleThread.html) ·
[RunsOn EC2 benchmarks](https://runs-on.com/benchmarks/aws-ec2-instances/).
Hyperscalers: the AWS on-demand and spot feeds behind the EC2 pricing pages, the
[Spot Advisor data](https://spot-bid-advisor.s3.amazonaws.com/spot-advisor-data.json), the
[Azure Retail Prices API](https://prices.azure.com/api/retail/prices),
[GCP general-purpose pricing](https://cloud.google.com/products/compute/pricing/general-purpose),
[GCP Spot pricing](https://cloud.google.com/spot-vms/pricing),
[GCP image pricing](https://cloud.google.com/compute/disks-image-pricing).
Bare metal: [Hetzner price adjustment, June 2026](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/) ·
[Hetzner AX](https://www.hetzner.com/dedicated-rootserver/matrix-ax-mobile/) ·
[Hetzner Windows pricing](https://docs.hetzner.com/robot/general/pricing/windows-2022-pricing/) ·
[OVH Rise US](https://eco.us.ovhcloud.com/rise/) · [OVH bare metal prices](https://us.ovhcloud.com/bare-metal/prices/) ·
[Scaleway Elastic Metal](https://www.scaleway.com/en/pricing/elastic-metal/) ·
[InterServer](https://www.interserver.net/dedicated/) · [RackNerd](https://www.racknerd.com/amd-ryzen-dedicated-servers) ·
[Vultr plans API](https://api.vultr.com/v2/plans-metal).
