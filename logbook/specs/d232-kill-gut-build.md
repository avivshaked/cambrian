# The build of D232: a kill swallows the whole body into the killer's gut

*2026-10-25, the speed agent, for the coordinator. Branch `kill-gut` off `main` at `b7e69a95`, in
`scratch/wt-killgut`. A Sonnet subagent builds it in `Evosim.Core` and I review it. The rule is D232 in
`DECISIONS.md`. Nothing changes on the card, since the meal is a rule of the world and runs on the host.*

## What is built

- **The switch** is a bool tunable beside the contact-eating dials, off by default, bound to `EVOSIM_KILL_GUT`. It
  requires contact eating (D219).
- **The kill.** When a bite kills a body, the killer recorded for that death takes the dead body's remaining tissue and
  store into its gut, with their matter, and no remains are made. A body that dies any other way leaves remains as
  today.
- **The digestion.** Each step, the gut passes into the store at most the mouth's per-step cap, less the intake
  waste. It is booked as a bite's intake is booked.
- **The killer's death.** What is in the gut goes with the body: into its remains, or into its own killer's gut.
- **The books** close with the switch on, at every step.
- **The checkpoint** holds each gut only when the switch is on.
- **The stats** gain the gut's columns only when the switch is on.

## The coordinator's readings of D232 (2026-10-25)

- *The gut shares the mouth's per-step cap with bites, and bites take theirs first.* A body has one mouth, so in a
  step where it bites and digests, the bites take their intake under the cap first, and the gut fills what the cap
  leaves. That is what D232 means by "the same per-step cap a bite meets". The other reading was set aside: a cap of
  its own for the gut would let a grazer that is digesting take in up to twice the mouth's rate.
- *Only a killer with a mouth swallows.* Swallowing is a mouth's act, and the gut is digested at the mouth's rate.
  So a killer whose mouths take nothing, a claw alone, leaves remains as today. As first built, a claw-only killer
  swallowed and could never digest, and claw kills vanished from the floor.
- *One re-recording of the crowd fixture* at the end covers every new tunable: D231's, D232's and level (b)'s
  enum value.
