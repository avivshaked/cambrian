# Round 34 reads on r34-s1 (agent's report, 2026-09-11, saved by the main session)

Run `runs/r34-s1/2026-09-10-215639-96b76bdc/`, ended budget, 17,707 bred births + 124 floor spawns.

## R1 ledger: a jointed genome against the same genome made rigid

Two jointed leaves alive at t=5,000 (snapshot 000005000): id 150 (founder 2, box root + link
capsule on a HingeTwist, 2 DOF, 7.425 N·m) and id 689 (founder 139, sphere root + link sphere on a
TwistHinge, 2 DOF, 5.683 N·m). Rigid copies: node joint set to Fixed, power 0, limits []; files
jointed.json / rigid.json / jointed2.json / rigid2.json; ledger output ledger-cladeA.txt, -cladeB.txt.
Command: ledger.ps1 -Genome <file> -Config <run>/config.json -Clearance 1,5,10 -Depth 0,12,20 -Density 0.5,1,2,4 -Compare.
Neither has absorptive tissue, so only depth moves the as-stored reading.

Clade A: depth 0: net 4.056 W jointed / 4.058 rigid, lifetime 5,270 / 5,332 s, R0 207 / 207.
depth 12: 1.043 / 1.045 W, 2,209 / 2,229 s, R0 21 / 21. depth 20: 0.190 / 0.191 W, 782 / 789 s, R0 0 / 0.
Clade B: depth 0: 0.227 / 0.228 W, 9,823 / 10,028 s, R0 23 / 24. depth 12: 0.073 / 0.074 W,
5,032 / 5,862 s, R0 4 / 4. depth 20: 0.029 / 0.030 W, 4,210 / 2,701 s, R0 0 / 1.
The gap is exactly idle × power × DOF (0.0015 W and 0.0011 W): a fraction of a percent of income.

## R2 births per parent (scripts/reads/parent-births.py; parent-births-r34-s1.txt, -r35-s1.txt)

"Parent" = every organism born in the window (zeros counted). Lifetimes right-censored at the file's end.

r34-s1 by own jnt flag: window / group / n / mean children / median / mean lifetime s / children per 1,000 s
0-5,000: jointed 573 / 1.499 / 0 / 661 / 1.28; rigid 1,582 / 1.485 / 0 / 1,245 / 0.80
5,000-15,000: jointed 661 / 0.649 / 0 / 511 / 0.24; rigid 4,409 / 1.361 / 0 / 1,541 / 0.38
0-30,000: jointed 1,404 / 0.942 / 0 / 542 / 0.64; rigid 16,427 / 0.997 / 0 / 1,373 / 0.30
r34-s1 by own abs flag, 0-30,000: absorptive 2,753 / 0.988 / 0 / 2,846 / 0.40; non-abs 15,078 / 0.994 / 0 / 1,027 / 0.31
r35-s1 (priced) by jnt: 0-5,000 jointed 100 / 0.100 / 0 / 67 s; none after 5,000 s (the script reads a world without joints).

## Anomalies (agent's)
- organisms recorded 17,831 = births 17,707 + 124 floor spawns.
- The joint lives on MorphNode (JointType, Power, JointLimits), not on MorphEdge.
- Only two jointed founder clades alive at 5,000 s (71 bodies of founder 2, 2 of founder 139).
- The harness refused the agent's write of this .md file; content saved by the main session.

## R3 age at death and birth size (parent-births.py --deaths; parent-births-deaths-r34-s1.txt)

Age at death, 0-30,000 s window (jointed n=1,404, rigid n=16,427): 0-50 s 33.8% / 14.9%;
50-200 s 28.8% / 24.3%; 200-500 s 16.9% / 18.5%; 500-1,500 s 9.8% / 11.0%; 1,500-3,000 s 5.2% / 9.0%;
3,000+ s 5.6% / 14.3%; alive at end 0 / 1,305 (7.9%). In 0-5,000 s: 41.0% of jointed newborns dead
within 50 s against 22.9% of rigid.
Birth rows, 0-5,000 s: jointed bf mean 0.123 median 0.074, as mean 0.964; rigid bf mean 0.329 median
0.347, as mean 0.892. 0-30,000 s: jointed bf 0.112 / 0.074, as 0.890; rigid bf 0.259 / 0.107, as 0.620.
Jointed newborns have a jointed parent 87-97% of the time; rigid newborns 1-4%.
Not one organism born with an expressed joint (1,404) was alive at 29,999.5 s.

## r34-s2 landed (2026-09-11, budget reached at 30,000 s)

Score PASS, clade root 151 (kind r, born 319), 139 alive at end, min last 6,000 s 86, 117 inherited births in last 20 samples; 7 living clades. Photo held on all three readings (1,588 alive, 1,015 inherited births).
jnt inh @1000 58 (jointed 64 of 194); @5100 57 (63 of 1,073); @10100 15; @15100 2; last jointed body at sample 17,400 (1); 0 from 17,500. Diverged 27 by 15,100, then none. cols 100/100 from 5,100. alive 1,593 at end.
M1 fails (no jointed body at the end), M2 holds (58 at 1,000 s). Divergence dump not yet read.

### r34-s2 divergence dumps (scripts/reads/diverged-read.py r34-s2 r34-s4; 2026-09-11)

27 dumps; 27/27 genomes carry an active joint node (joint != Fixed, power > 0); 25/27 phenotypes jointed (567 and 4306 had the jointed node pruned in development: parts 2, totalDof 0). Median age at divergence 1,182 s; 4 under 100 s (1296 @16.5 s, 2281 @31 s, 3176 @2 s, 3836 @95.5 s); t range 1,181 to 11,350 s. 24 root y NaN; 3 finite but astronomical (255 y=-1.07e10 at 454 s; 656 y=5,487 m; 1259 y=-895 m). No dump failed on a non-root link with the root intact.
r34-s4 (running): 2 dumps at 2,034.5 s and 5,978.5 s, both jointed, ages 1,550 and 5,150 s.
Reading: as in s1 (3, all jointed) and s5 (17, 16 jointed adults): the solver throws jointed adults, median age over a thousand seconds, so divergence is a jointed adult's death and not a newborn's. 27 among ~1,400 jointed births is ~2%: a real leak, not the thinning's cause.

## r34-s4 landed (2026-09-11 ~12:20, budget reached at 30,000 s; run 2026-09-11-011311-96b76bdc)

Score PASS, clade root 2029 (kind r, born 3,707), 47 alive at end, min last 6,000 s 46, 17 inherited births in last 20 samples; 3 living clades.
jnt inh @1000 46 (jointed 50 of 358); @5100 34 (36 of 1,446); @10100 1; last jointed body at sample 10,800 (1); 0 from 10,900. Diverged 2 (both jointed adults, 1,550 and 5,150 s old). cols 100/100 from 5,100. alive 1,766 at end.
M1 fails, M2 holds. Round 34 complete: 5/5 PASS (206, 69, 115, 139, 47); M1 fails 5/5 (last jointed body at 8,300-20,800 s); M2 holds 5/5 (jnt inh @1000: 73, 58, 58, 46, 49... check s1/s3/s5 figures against the earlier table before writing).

## Round 34 predictions, all five seeds (2026-09-11 12:40)

M0 alive @30000 r34/r35 same seed: s1 1,305/1,270 (1.03); s2 1,593/1,613 (0.99); s3 1,557/1,671 (0.93); s4 1,766/1,709 (1.03); s5 1,281/1,380 (0.93). HOLDS 5/5 (0080's amendment reads round 35, not 32).
M1 jnt inh/alive @30000: 0 in every seed; last jointed body s1 20,800, s2 17,400, s3 8,300 (check), s4 10,800, s5 (check earlier table). FAILS 5/5.
M2 jnt inh @5000 vs @1000: s1 73/72, s2 57/58, s3 17/84 (0.20, fails), s4 34/46 (0.74), s5 49/140 (0.35). HOLDS 4/5 (the bar was 3 of 5).
M3 diverged per arm: s1 3, s2 27, s3 0, s4 2, s5 17. FAILS (3 of 5 above 2); every dump read carries an active joint; median age ~1,200 s (s2), adults.
M4 (conditional on M1, so moot as written) read anyway over 1,000-9,000 s: spd jnt/spd rig s1 1.06-1.18; s2 1.11-1.52; s3 1.06-1.15; s4 1.37-1.55; s5 1.16-1.26; spd rig equals mean m/s (the water, 0.074-0.09). Jointed bodies move 10-50% faster than the water while they exist: the free stroke is used.
Two-sided reading that fires: "M2 holds and M1 fails" (free joints survive founding and are lost slowly), plus M3 fails (the solver is part of the answer). R1-R3 reads (ledger, parent-births, drift arithmetic) are above.
Last sample with an inherited jointed body (jnt inh >= 1): s1 23,100; s2 17,000; s3 8,200; s4 10,800; s5 14,900. After it, samples where a jointed body exists with none inherited (a fresh mutant, dying before it breeds a jointed child): s1 68 samples (last 28,900), s2 18 (26,700), s3 26 (29,100), s4 0, s5 19 (28,400). So the joint keeps being re-invented by mutation every few hundred seconds to the end and never re-founds a line.
