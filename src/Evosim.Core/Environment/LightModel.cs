using System;

namespace Evosim.Core
{
    /// <summary>
    /// How much light reaches a given depth — DESIGN.md §5A.4.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Light is the world's only primary energy input (§5A.2)</b>, so this is where every
    /// joule in the ecosystem comes from. Nutrients are recycled dead matter and predation moves
    /// energy sideways; nothing else adds any.
    /// </para>
    /// <para>
    /// <b>Beer–Lambert, because the alternative has no shape.</b> Irradiance falls exponentially
    /// with depth: each metre of water removes a fixed <i>fraction</i>, not a fixed amount. A
    /// linear falloff would reach exactly zero at a particular depth and be meaningless below it,
    /// which puts a hard floor under the world at a place we chose. Exponential decay has no such
    /// edge — it gets arbitrarily dark without ever becoming a different regime — so how deep the
    /// photosynthetic niche reaches is set by the economics of §5A.2 rather than by a constant
    /// here.
    /// </para>
    /// <para>
    /// This is the cheapest source of <b>spatial heterogeneity</b> in the design, and §5A.4 wants
    /// heterogeneity because it is what stops one strategy winning everywhere: the surface is the
    /// photosynthetic niche and the depths are the detritus niche, maintained by geometry rather
    /// than by tuning.
    /// </para>
    /// <para>
    /// <b>There is a diurnal cycle, and it is off by default.</b> §5A.4 wants one, and it is what
    /// makes diel vertical migration expressible. The objection to adding it was that a cycle
    /// turns §5A.2's calibration question from "does light cover upkeep" into "does light cover
    /// upkeep <i>averaged over a period</i>, and can anything survive the trough" — two unknowns
    /// at once, before either had been measured alone.
    /// </para>
    /// <para>
    /// <b>That objection is answered by making the cycle mean-preserving</b>, not by deferring it.
    /// <see cref="SurfaceIrradiance"/> stays the daily <i>mean</i> and
    /// <see cref="DayNightAmplitude"/> modulates around it, so 0 reproduces the acyclic world
    /// exactly and turning it up does not move the world's energy budget by one joule. One
    /// unknown, and it has a defined zero.
    /// </para>
    /// <para>
    /// <b>What a cycle does and does not buy.</b> It does not on its own move the best place to
    /// be: irradiance is monotonically decreasing in depth at every hour, so the surface always
    /// wins on light alone. What it moves is the <i>balance</i> against the other income.
    /// Detritus sinks (§5A.2c), so nutrients are deep and light is shallow, and a creature earning
    /// from both has an optimum somewhere between. Darken the surface and that optimum descends;
    /// brighten it and it rises. A migrator tracks it and a sitter takes the average of both —
    /// which is diel vertical migration, arrived at from the economics rather than installed.
    /// <i>(Author's inference. Real DVM is usually attributed to predation risk, which this world
    /// does not have yet.)</i>
    /// </para>
    /// <para>
    /// <b>Mean-preserving in light, not in income.</b> The cycles keep the mean of the
    /// <i>light</i> a surface receives, and nothing more. Under D098 a leaf's income is
    /// <c>min(light, uptake ceiling)</c>, so a leaf bound by its uptake ceiling loses the peak
    /// and keeps the trough: the same mean light pays it less. A cycle therefore moves the
    /// world's income whenever leaves are uptake-bound, and a run that switches one on has to
    /// read income, not light, to see what it did.
    /// </para>
    /// <para>
    /// <b>A season multiplies the day.</b> <see cref="SeasonAmplitude"/> and
    /// <see cref="SeasonLengthSeconds"/> add a slower sinusoid, and
    /// <see cref="DayFactorAt"/> is the product of the two factors. The product keeps the mean of
    /// 1 only when the cross term averages to zero, which holds when the season is a whole number
    /// of days (at least two) long; the world refuses a config with both on and any other ratio.
    /// Season amplitude 0 is the day-only world exactly.
    /// </para>
    /// <para>
    /// <b>Two seasons, and weather</b> (D151). <see cref="SeasonShape"/> swaps the sinusoid for a
    /// summer and a winter switched at midnights over a smooth ramp, and <see cref="WeatherSpread"/>
    /// multiplies a log-normal factor a day with mean 1, drawn from its own seed. The light is
    /// <c>season × day × weather</c>, still a pure function of the clock and the config. At the
    /// smooth shape and a spread of 0 every factor is the bit pattern the build before returned.
    /// </para>
    /// </remarks>
    public sealed class LightModel
    {
        /// <summary>Irradiance just below the surface, W/m².</summary>
        /// <remarks>
        /// ⚠ Unmeasured (§5A.10), and half of the ratio §5A.2 calls the knob that decides
        /// everything. Its absolute value means little on its own — what matters is this against
        /// <see cref="PhotosyntheticCell.Efficiency"/> and against cell upkeep.
        /// </remarks>
        [Tunable("light", Unit = "W/m2")]
        public float SurfaceIrradiance
        {
            get => _surfaceIrradiance;
            set => _surfaceIrradiance = Require(value, nameof(SurfaceIrradiance), NoLight);
        }

        private float _surfaceIrradiance;

        /// <summary>
        /// Depth over which irradiance falls to 1/e of its surface value, in metres.
        /// </summary>
        /// <remarks>
        /// Clear ocean water attenuates over tens of metres; coastal water over a few. The value
        /// here is a world-design choice rather than a physical measurement: it sets how far a
        /// creature must travel to leave the lit zone, and therefore how much vertical structure
        /// the world has to offer. ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("light", Unit = "m")]
        public float AttenuationDepth
        {
            get => _attenuationDepth;
            set => _attenuationDepth = Require(value, nameof(AttenuationDepth), NoDepth);
        }

        private float _attenuationDepth;

        /// <summary>
        /// How far surface irradiance swings either side of its mean over a day, as a fraction —
        /// DESIGN.md §5A.4. 0 is a world with no night.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The cycle is mean-preserving by construction</b>, so this dial does not change how
        /// much energy the world receives in a day, only when it arrives. At 1 the surface runs
        /// from dark to twice the mean; at 0 it sits at the mean forever and every number measured
        /// under the acyclic model still means what it meant.
        /// </para>
        /// <para>
        /// A sinusoid rather than a clamped one. <c>max(0, sin)</c> would give a true half-day
        /// night, and it averages to 1/π of its peak — so switching it on at a fixed
        /// <see cref="SurfaceIrradiance"/> would quietly cut the world's income to a third and
        /// present as a diurnal effect. Preserving the mean is what keeps this one unknown rather
        /// than two.
        /// </para>
        /// <para>⚠ Unmeasured (§5A.10). Default 0: no night until a run asks for one.</para>
        /// </remarks>
        [Tunable("light")]
        public float DayNightAmplitude
        {
            get => _dayNightAmplitude;
            set => _dayNightAmplitude = value >= 0f && value <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(DayNightAmplitude), value,
                    "Must be in [0, 1]. Above 1 the surface goes dark for part of the day and " +
                    "the cycle stops preserving its own mean, which is the whole reason it can " +
                    "be switched on without recalibrating the world.");
        }

        private float _dayNightAmplitude;

        /// <summary>Length of one day, in simulated seconds — DESIGN.md §5A.4.</summary>
        /// <remarks>
        /// <b>The period a creature has to outlive, and to anticipate.</b> Too short and no body
        /// can cross a meaningful part of the water column within one, so the cycle is noise that
        /// can only be averaged over; too long and a run contains no days at all. It only means
        /// anything read against how far a creature can actually swim in one, which today is very
        /// little. ⚠ Unmeasured (§5A.10).
        /// </remarks>
        [Tunable("light", Unit = "s")]
        public float DayLengthSeconds
        {
            get => _dayLengthSeconds;
            set => _dayLengthSeconds = Require(value, nameof(DayLengthSeconds), NoDay);
        }

        private float _dayLengthSeconds = 200f;

        /// <summary>
        /// How far the season swings surface irradiance either side of its mean, as a fraction.
        /// 0 is a world with no seasons.
        /// </summary>
        /// <remarks>
        /// The slow cycle that multiplies the day's. Mean-preserving in light on the same terms as
        /// <see cref="DayNightAmplitude"/>, and with both on only when
        /// <see cref="SeasonLengthSeconds"/> is a whole number (at least 2) of
        /// <see cref="DayLengthSeconds"/>; see the class remarks. Default 0.
        /// </remarks>
        [Tunable("light")]
        public float SeasonAmplitude
        {
            get => _seasonAmplitude;
            set => _seasonAmplitude = value >= 0f && value <= 1f
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(SeasonAmplitude), value,
                    "Must be in [0, 1]. Above 1 the surface goes dark for part of the season and " +
                    "the cycle stops preserving its own mean.");
        }

        private float _seasonAmplitude;

        /// <summary>Length of one season cycle, in simulated seconds.</summary>
        /// <remarks>
        /// With a day cycle also on, a whole number (at least 2) of days, so the product's mean
        /// stays 1. Only read when <see cref="SeasonAmplitude"/> is above 0.
        /// </remarks>
        [Tunable("light", Unit = "s")]
        public float SeasonLengthSeconds
        {
            get => _seasonLengthSeconds;
            set => _seasonLengthSeconds = Require(value, nameof(SeasonLengthSeconds), NoSeason);
        }

        private float _seasonLengthSeconds = 30000f;

        /// <summary>
        /// The shape of the season: <see cref="Core.SeasonShape.Smooth"/>'s sinusoid or
        /// <see cref="Core.SeasonShape.TwoSeason"/>'s summer and winter (D151). Default smooth.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Under the two-season shape the season factor is <c>1 + A·s(t)</c>, with <c>s</c> +1 in
        /// summer and −1 in winter. With <c>Y</c> the year and <c>D</c> the day, summer runs from
        /// <c>−D/4</c> to <c>Y/2 − D/4</c> (mod <c>Y</c>), so each switch is centred on a midnight,
        /// the darkest point of the day sine, and a run opens in summer. Each switch runs over
        /// <see cref="SeasonRampSeconds"/> through a smoothstep that is odd about its centre; the
        /// day sine is even about a midnight, so their cross term cancels and the year's mean light
        /// is exactly the mean. With a day cycle on, the world refuses a two-season year that is not
        /// an even number of days (at least 2), so each half holds whole days.
        /// </para>
        /// </remarks>
        [Tunable("light")]
        public SeasonShape SeasonShape
        {
            get => _seasonShape;
            set => _seasonShape = Enum.IsDefined(typeof(SeasonShape), value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(SeasonShape), value, "Must be Smooth or TwoSeason.");
        }

        private SeasonShape _seasonShape = SeasonShape.Smooth;

        /// <summary>
        /// Seconds over which the two-season light moves from one season's level to the other.
        /// 0 is an instant switch. Read only under <see cref="Core.SeasonShape.TwoSeason"/>.
        /// </summary>
        /// <remarks>
        /// At least 0 and finite here; the world refuses a ramp of half a season (a quarter of
        /// <see cref="SeasonLengthSeconds"/>) or more, because the bound needs the year's length
        /// and a setter cannot rely on the order the two are set in. Default 0.
        /// </remarks>
        [Tunable("light", Unit = "s")]
        public float SeasonRampSeconds
        {
            get => _seasonRampSeconds;
            set => _seasonRampSeconds = value >= 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(SeasonRampSeconds), value,
                    "Must be finite and at least 0. 0 switches the season at an instant.");
        }

        private float _seasonRampSeconds;

        /// <summary>
        /// The weather: the standard deviation of a day's log factor. 0 is no weather.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Day <c>n</c> runs from midnight to midnight, <c>n = floor((t + D/4) / D)</c>. Its log
        /// factor <c>x_n</c> is a stationary AR(1) process with mean <c>μ = −σ²/2</c>, so that the
        /// factor <c>exp(x_n)</c> has mean 1 and the expected light is unchanged:
        /// <c>x_0 = μ + σ·ε_0</c> and <c>x_n = μ + ρ·(x_{n−1} − μ) + σ·√(1 − ρ²)·ε_n</c>, the
        /// <c>ε</c> drawn in day order from <c>new Rng(WeatherSeed)</c>. A day's factor blends to
        /// the next by the season's smoothstep over the quarter-day centred on the midnight
        /// between them, so the change happens in the dark.
        /// </para>
        /// <para>
        /// The world refuses weather without a day cycle. Default 0, which draws nothing and is
        /// exactly 1.
        /// </para>
        /// </remarks>
        [Tunable("light")]
        public float WeatherSpread
        {
            get => _weatherSpread;
            set => _weatherSpread = value >= 0f && !float.IsInfinity(value)
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(WeatherSpread), value,
                    "Must be finite and at least 0: it is the standard deviation of a day's log " +
                    "light factor, and 0 is a world without weather.");
        }

        private float _weatherSpread;

        /// <summary>The day-to-day correlation of the weather's log factor, in [0, 1). Default 0.</summary>
        /// <remarks>
        /// Dull days come in spells as this rises. 1 would be a sky fixed by its first day and
        /// leaves the process with no stationary spread, so it is refused.
        /// </remarks>
        [Tunable("light")]
        public float WeatherCorrelation
        {
            get => _weatherCorrelation;
            set => _weatherCorrelation = value >= 0f && value < 1f
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(WeatherCorrelation), value,
                    "Must be in [0, 1). At 1 every day is the first day's weather and the series " +
                    "has no stationary spread; below 0 it would alternate dull and bright days.");
        }

        private float _weatherCorrelation;

        /// <summary>
        /// The seed of the weather series alone, so that every seed of a round has the same sky.
        /// Default 1.
        /// </summary>
        /// <remarks>
        /// At most 2^53, because <c>config.json</c> carries its numbers as doubles and a larger seed
        /// would not come back from the file as the seed that was written.
        /// </remarks>
        [Tunable("light")]
        public ulong WeatherSeed
        {
            get => _weatherSeed;
            set => _weatherSeed = value <= MaxWeatherSeed
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(WeatherSeed), value,
                    "Must be at most 2^53 (9007199254740992): config.json carries its numbers as " +
                    "doubles, and a larger seed would not read back as the seed that was written.");
        }

        private ulong _weatherSeed = 1UL;

        /// <summary>The largest seed a double carries exactly, and so the largest the file can.</summary>
        public const ulong MaxWeatherSeed = 1UL << 53;

        public LightModel(float surfaceIrradiance = 400f, float attenuationDepth = 12f)
        {
            SurfaceIrradiance = surfaceIrradiance;
            AttenuationDepth = attenuationDepth;
        }

        /// <summary>
        /// The multiplier on <see cref="SurfaceIrradiance"/> at a point in time. 1 with no cycle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A pure function of absolute elapsed seconds, and deliberately not a mutable phase this
        /// object carries. <b>This class is configuration</b> — every other member on it is a
        /// <c>[Tunable]</c>, it is what §7 hashes, and where the world has got to is not part of
        /// how the world was set up. <see cref="LightField"/> already holds the light's per-step
        /// state and holds this too.
        /// </para>
        /// <para>
        /// Absolute seconds rather than a delta, so a caller that skips a step or calls twice
        /// cannot walk the sun out of phase with the world that is paying for it. That failure
        /// would present as a slow trend nobody chose, which is the kind this project keeps
        /// paying for.
        /// </para>
        /// </remarks>
        public float DayFactorAt(double elapsedSeconds)
        {
            // Without weather, the arithmetic below is round 56's exactly, smooth shape and all:
            // the two-season branch is new and reached only by a config that asks for it. A
            // weather factor multiplies the same double product before the one cast.
            bool weather = WeatherSpread > 0f;

            if (SeasonAmplitude <= 0f)
            {
                if (!weather)
                {
                    return DayNightAmplitude <= 0f
                        ? 1f
                        : (float)(1.0 + DayNightAmplitude *
                            Math.Sin(2.0 * Math.PI * elapsedSeconds / DayLengthSeconds));
                }

                double dayOnly = DayNightAmplitude <= 0f
                    ? 1.0
                    : 1.0 + DayNightAmplitude *
                        Math.Sin(2.0 * Math.PI * elapsedSeconds / DayLengthSeconds);
                return (float)(dayOnly * WeatherFactorAt(elapsedSeconds));
            }

            double season = SeasonShape == SeasonShape.TwoSeason
                ? 1.0 + SeasonAmplitude * TwoSeasonSign(elapsedSeconds)
                : 1.0 + SeasonAmplitude *
                    Math.Sin(2.0 * Math.PI * elapsedSeconds / SeasonLengthSeconds);
            double day = DayNightAmplitude <= 0f
                ? 1.0
                : 1.0 + DayNightAmplitude *
                    Math.Sin(2.0 * Math.PI * elapsedSeconds / DayLengthSeconds);

            return weather
                ? (float)(season * day * WeatherFactorAt(elapsedSeconds))
                : (float)(season * day);
        }

        /// <summary>
        /// The two-season square wave at a point in time: +1 in summer, −1 in winter, and the
        /// smoothstep between them over <see cref="SeasonRampSeconds"/> at each switch.
        /// </summary>
        /// <remarks>
        /// <c>u = (t + D/4) mod Y</c> puts the winter-to-summer switch at <c>u = 0</c> and the
        /// summer-to-winter one at <c>u = Y/2</c>, each on a midnight when the year is whole days.
        /// Each ramp is odd about its switch, so the year's mean of the sign is exactly 0.
        /// </remarks>
        public double TwoSeasonSign(double elapsedSeconds)
        {
            double year = SeasonLengthSeconds;
            double half = 0.5 * year;
            double ramp = SeasonRampSeconds;

            double u = (elapsedSeconds + 0.25 * (double)DayLengthSeconds) % year;
            if (u < 0.0) u += year;

            if (ramp > 0.0)
            {
                double halfRamp = 0.5 * ramp;
                if (u < halfRamp) return OddStep(u / ramp);
                if (u > year - halfRamp) return OddStep((u - year) / ramp);
                if (Math.Abs(u - half) < halfRamp) return -OddStep((u - half) / ramp);
            }

            return u < half ? 1.0 : -1.0;
        }

        /// <summary>
        /// The smoothstep from −1 to +1 over <c>x</c> in [−1/2, 1/2], odd about 0; ±1 outside.
        /// </summary>
        private static double OddStep(double x)
        {
            if (x <= -0.5) return -1.0;
            if (x >= 0.5) return 1.0;
            double p = x + 0.5;
            return 2.0 * (p * p * (3.0 - 2.0 * p)) - 1.0;
        }

        /// <summary>The smoothstep from 0 to 1 over <c>p</c> in [0, 1].</summary>
        private static double SmoothStep(double p)
        {
            if (p <= 0.0) return 0.0;
            if (p >= 1.0) return 1.0;
            return p * p * (3.0 - 2.0 * p);
        }

        /// <summary>
        /// The weather's multiplier at a point in time: day <c>n</c>'s factor, blended to the next
        /// over the quarter-day centred on the midnight between them. Exactly 1 at a spread of 0.
        /// </summary>
        public double WeatherFactorAt(double elapsedSeconds)
        {
            if (!(WeatherSpread > 0f)) return 1.0;

            double dayLength = DayLengthSeconds;
            double sinceMidnight = elapsedSeconds + 0.25 * dayLength;
            double dayIndex = Math.Floor(sinceMidnight / dayLength);
            double intoDay = sinceMidnight - dayIndex * dayLength;
            long n = dayIndex < 0.0 ? 0L : (long)dayIndex;

            double eighth = 0.125 * dayLength;
            double quarter = 0.25 * dayLength;

            if (intoDay > dayLength - eighth)
            {
                double w = SmoothStep((intoDay - (dayLength - eighth)) / quarter);
                double here = WeatherFactorOfDay(n);
                return here + w * (WeatherFactorOfDay(n + 1) - here);
            }

            if (intoDay < eighth && n > 0)
            {
                double w = SmoothStep((intoDay + eighth) / quarter);
                double before = WeatherFactorOfDay(n - 1);
                return before + w * (WeatherFactorOfDay(n) - before);
            }

            return WeatherFactorOfDay(n);
        }

        /// <summary>Day <paramref name="day"/>'s weather factor, <c>exp(x_n)</c>. 1 with no weather.</summary>
        public double WeatherFactorOfDay(long day) => Math.Exp(WeatherLogFactorOfDay(day));

        /// <summary>Day <paramref name="day"/>'s log factor <c>x_n</c>. 0 with no weather.</summary>
        /// <remarks>
        /// <para>
        /// The series is drawn once, lazily, in day order, and extended as later days are asked
        /// for, so the order of calls cannot change a value: day <c>n</c> is always the
        /// <c>n</c>-th step of the same recurrence from the same seed. The cache is a function of
        /// the config alone (<see cref="WeatherSpread"/>, <see cref="WeatherCorrelation"/> and
        /// <see cref="WeatherSeed"/>), keyed on all three and rebuilt if any of them changes, so it
        /// adds nothing to a checkpoint. Locked, because one model may be read by more than one
        /// world.
        /// </para>
        /// </remarks>
        public double WeatherLogFactorOfDay(long day)
        {
            if (day < 0) throw new ArgumentOutOfRangeException(nameof(day), day, "A day is at least 0.");
            if (!(WeatherSpread > 0f)) return 0.0;

            lock (_weatherLock)
            {
                if (_weatherLog == null || _weatherCachedSpread != WeatherSpread ||
                    _weatherCachedCorrelation != WeatherCorrelation || _weatherCachedSeed != WeatherSeed)
                {
                    _weatherCachedSpread = WeatherSpread;
                    _weatherCachedCorrelation = WeatherCorrelation;
                    _weatherCachedSeed = WeatherSeed;
                    _weatherRng = new Rng(WeatherSeed);
                    _weatherLog = new System.Collections.Generic.List<double>();
                }

                double sigma = _weatherCachedSpread;
                double rho = _weatherCachedCorrelation;
                double mu = -0.5 * sigma * sigma;
                double innovation = sigma * Math.Sqrt(1.0 - rho * rho);

                while (_weatherLog.Count <= day)
                {
                    double epsilon = _weatherRng.Gaussian();
                    double x = _weatherLog.Count == 0
                        ? mu + sigma * epsilon
                        : mu + rho * (_weatherLog[_weatherLog.Count - 1] - mu) + innovation * epsilon;
                    _weatherLog.Add(x);
                }

                return _weatherLog[(int)day];
            }
        }

        private readonly object _weatherLock = new object();
        private System.Collections.Generic.List<double> _weatherLog;
        private Rng _weatherRng;
        private float _weatherCachedSpread;
        private float _weatherCachedCorrelation;
        private ulong _weatherCachedSeed;

        /// <remarks>
        /// Settable as well as constructable, because §5A.10's rule is that an unmeasured number
        /// must be sweepable, and both of these are unmeasured. Validated on the way in either
        /// route, so a config loaded from a hand-edited file cannot introduce a world with no sun.
        /// </remarks>
        private static float Require(float value, string name, string why)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, why);
            }
            return value;
        }

        private const string NoLight =
            "A world with no light has no primary energy input and nothing can live in it.";

        private const string NoDay =
            "A day of zero or infinite length is not a cycle, and the sun would sit at one " +
            "instant of it forever.";

        private const string NoSeason =
            "A season of zero or infinite length is not a cycle.";

        private const string NoDepth =
            "Light must fall off over some distance, or depth means nothing and the world has " +
            "no vertical structure at all.";

        /// <summary>
        /// Irradiance at a world height, W/m². Y is up, so the surface is 0 and depths are
        /// negative.
        /// </summary>
        /// <remarks>
        /// Above the surface reads as the surface value rather than more: a creature that
        /// breached would otherwise gain energy by leaving the water, which is a free-energy
        /// source of exactly the kind §11.2 exists to catch. Nothing can breach today — buoyancy
        /// is neutral and there is no air — but the clamp costs one comparison and removes the
        /// question.
        /// </remarks>
        public float IrradianceAt(float heightY)
        {
            if (heightY >= 0f) return SurfaceIrradiance;

            return SurfaceIrradiance * (float)Math.Exp(heightY / AttenuationDepth);
        }

        /// <summary>Depth, in metres, below which irradiance is under the given fraction.</summary>
        /// <remarks>
        /// For reporting rather than for simulation: "the lit zone is N metres deep" is the sort
        /// of statement a run summary should be able to make without the reader deriving it.
        /// </remarks>
        public float DepthForFraction(float fraction)
        {
            if (!(fraction > 0f) || fraction >= 1f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fraction), fraction, "Must be in (0, 1).");
            }

            return -AttenuationDepth * (float)Math.Log(fraction);
        }

        // HashContribution() was removed rather than extended. Nothing called it: the two
        // properties it hand-wrote both carry [Tunable] and reach 7's hash through
        // ConfigSchema (D027), so it was a second copy of the same declaration free to
        // diverge. Adding the two knobs above to it would have made that copy look
        // maintained.

        public override string ToString() =>
            FormattableString.Invariant(
                $"{SurfaceIrradiance:0} W/m² at surface, 1/e at {AttenuationDepth:0.#} m");
    }

    /// <summary>The shape of <see cref="LightModel"/>'s season. Serialized by name.</summary>
    public enum SeasonShape
    {
        /// <summary>A sinusoid over the year: round 56's season, and every season before it.</summary>
        Smooth,

        /// <summary>Summer and winter at their own levels, with a smooth ramp between (D151).</summary>
        TwoSeason,
    }
}
