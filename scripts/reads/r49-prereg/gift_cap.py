"""Round 49 prereg helper: the owner's two alternatives for the one-part pool stomach,
on stomach_founder.py's arithmetic. 'g': the reserve after the first growth is capped at a fraction
of the gate (purse and endowment together). 'm': round 48's reserve, no child for N seconds."""
import stomach_founder as s

def run(option, rho, frac=0.9, moratorium=600.0, t_max=6000.0):
    tissue = s.BF * s.T_ADULT
    sw_n = s.BF * s.SW_ADULT
    reserve = s.PURSE * s.BF + s.ENDOW_S * sw_n
    age = 0.0; first = None; children = 0; capped = False
    while age < t_max:
        wear = 1.0 + age / s.SENESCENCE
        sw = s.SW_ADULT * (tissue / s.T_ADULT) * wear
        food = s.CLEAR * s.V_ABS * (tissue / s.T_ADULT) * rho
        reserve += food * s.DT
        reserve -= (sw + s.HANDLING * food) * s.DT
        age += s.DT
        if reserve <= 0.0:
            return first, children, age
        remaining = s.T_ADULT - tissue
        if remaining > 0.0:
            target = min(reserve - 0.1 * tissue, remaining)
            if target > 0.0:
                tissue += target; reserve -= target
        share = s.BI * tissue
        price = share + s.overhead(0.8 * share)
        gate = price + s.MARGIN * s.SW_ADULT * (tissue / s.T_ADULT) * wear
        if option == 'g' and not capped:
            reserve = min(reserve, frac * gate); capped = True
        if option == 'm' and age < moratorium:
            continue
        if reserve >= gate:
            reserve -= price; children += 1
            if first is None: first = age
    return first, children, None

print('%-6s %-8s %12s %9s %12s' % ('rho', 'opt', 'first child', 'children', 'died at'))
for rho in (0.2, 0.3, 0.4, 0.44, 0.5, 0.75, 1.0, 2.0):
    for opt, kw in (('g0.9', dict(option='g', frac=0.9)), ('g0.8', dict(option='g', frac=0.8)), ('m600', dict(option='m'))):
        f, n, d = run(rho=rho, **kw)
        print('%-6g %-8s %12s %9d %12s' % (rho, opt, '-' if f is None else '%g s' % f, n, '-' if d is None else '%g s' % d))
