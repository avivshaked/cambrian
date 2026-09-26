"""Round 49 prereg helper (not for the record): the one-part pool stomach (inocula/pool-r47/
stomach-r45s1-100.json) as a founder on round 48's prices, at a constant density at its mouth,
under the three endowment options. Arithmetic from World.Step's order (Metabolise, Grow,
Reproduce) and the ledger's numbers for this body (tissue 44.952 J, standing cost 0.3596 W,
clearance 10 m3/s per m3 over 0.0899 m3, handling 0.1 J per J eaten), not the ledger itself,
which starts a newborn at full size and knows no founder's purse or endowment.

  (a) keep:        the reserve starts at the purse (200 J x bf) plus the endowment (600 s of the
                   newborn's standing watts), and the gate reads it all;
  (b) upkeep only: the endowment is an account that pays upkeep first and is never read by the
                   gate or by growth;
  (c) drop:        the reserve is the purse alone.

Senescence wears upkeep only (D121): standing watts times 1 + age / 3000 s. The margin term of
the gate is the genome's 88.28 s of the standing watts at the body's age.
Usage: python stomach_founder.py"""

T_ADULT = 44.952
SW_ADULT = 0.3596
BF = 0.33226845
BI = 0.41533542
MARGIN = 88.28237
V_ABS = 0.0899
CLEAR = 10.0
HANDLING = 0.1
PURSE = 200.0
ENDOW_S = 600.0
DT = 0.5
FLOOR = 50.0
SENESCENCE = 3000.0


def overhead(child_tissue):
    return max(FLOOR, 2.0 * child_tissue)


def run(option, rho, t_max=6000.0):
    tissue = BF * T_ADULT
    sw_n = BF * SW_ADULT
    endow = ENDOW_S * sw_n
    reserve = PURSE * BF + (endow if option == 'a' else 0.0)
    account = endow if option == 'b' else 0.0
    age = 0.0
    first = None
    children = 0
    while age < t_max:
        wear = 1.0 + age / SENESCENCE
        sw = SW_ADULT * (tissue / T_ADULT) * wear
        food = CLEAR * V_ABS * (tissue / T_ADULT) * rho
        cost = (sw + HANDLING * food) * DT
        reserve += food * DT
        if option == 'b' and account > 0.0:
            paid = min(account, cost)
            account -= paid
            cost -= paid
        reserve -= cost
        age += DT
        if reserve <= 0.0:
            return first, children, age
        # Grow: target = reserve - 0.1 tissue, capped at what is left to grow.
        remaining = T_ADULT - tissue
        if remaining > 0.0:
            target = min(reserve - 0.1 * tissue, remaining)
            if target > 0.0:
                tissue += target
                reserve -= target
        # Reproduce: the lump gate at the body's own tissue and age.
        share = BI * tissue
        price = share + overhead(0.8 * share)
        gate = price + MARGIN * SW_ADULT * (tissue / T_ADULT) * wear
        if reserve >= gate:
            reserve -= price
            children += 1
            if first is None:
                first = age
    return first, children, None


if __name__ == '__main__':
    print('bf %.4f, newborn tissue %.2f J, purse %.2f J, endowment %.2f J, adult gate at age 0 %.2f J'
          % (BF, BF * T_ADULT, PURSE * BF, ENDOW_S * BF * SW_ADULT,
             BI * T_ADULT + overhead(0.8 * BI * T_ADULT) + MARGIN * SW_ADULT))
    print('%-6s %-4s %12s %9s %12s' % ('rho', 'opt', 'first child', 'children', 'died at'))
    for rho in (0.1, 0.2, 0.3, 0.37, 0.4, 0.44, 0.5, 0.75, 1.0, 1.27, 2.0, 2.2):
        for opt in 'abc':
            first, n, died = run(opt, rho)
            print('%-6g %-4s %12s %9d %12s' % (rho, opt, '-' if first is None else '%g s' % first, n,
                                                '-' if died is None else '%g s' % died))
