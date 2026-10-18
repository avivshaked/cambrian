using System;
using Evosim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D151's light: the two-season year, its ramp, and the weather
    /// (<c>proposals/propose-r57-weather.md</c>, <c>logbook/specs/r57-build/build-brief.md</c>).
    /// </summary>
    public sealed class SeasonWeatherTests
    {
        private readonly ITestOutputHelper _output;

        public SeasonWeatherTests(ITestOutputHelper output) => _output = output;

        /// <summary>Round 57's light: two seasons of 3,000 s at depth 0.6, a 250 s ramp, round 56's day.</summary>
        private static LightModel Round57(float ramp = 250f, float day = 0.5f, float spread = 0f) =>
            new LightModel(48f, 12f)
            {
                DayNightAmplitude = day, DayLengthSeconds = 1000f,
                SeasonAmplitude = 0.6f, SeasonLengthSeconds = 6000f,
                SeasonShape = SeasonShape.TwoSeason, SeasonRampSeconds = ramp,
                WeatherSpread = spread, WeatherCorrelation = 0.5f, WeatherSeed = 1UL,
            };

        // ------------------------------------------------------------------ the smooth shape

        [Fact]
        public void TheSmoothShapeAtRound56sSettingsIsTodaysArithmeticBitForBit()
        {
            // Round 56: a day of 1,000 s at 0.5 and a year of 10,000 s at 0.5. A ramp is not read
            // under the smooth shape, so one set here changes nothing either.
            var model = new LightModel(48f, 12f)
            {
                DayNightAmplitude = 0.5f, DayLengthSeconds = 1000f,
                SeasonAmplitude = 0.5f, SeasonLengthSeconds = 10000f,
            };
            var ramped = new LightModel(48f, 12f)
            {
                DayNightAmplitude = 0.5f, DayLengthSeconds = 1000f,
                SeasonAmplitude = 0.5f, SeasonLengthSeconds = 10000f,
                SeasonRampSeconds = 250f, WeatherCorrelation = 0.5f, WeatherSeed = 7UL,
            };

            Assert.Equal(SeasonShape.Smooth, model.SeasonShape);
            Assert.Equal(0f, model.WeatherSpread);

            for (int i = 0; i <= 200_000; i++)
            {
                double t = i * 0.05;

                // The build before's DayFactorAt with both cycles on, written out.
                double season = 1.0 + 0.5f * Math.Sin(2.0 * Math.PI * t / 10000f);
                double day = 1.0 + 0.5f * Math.Sin(2.0 * Math.PI * t / 1000f);
                int expected = BitConverter.SingleToInt32Bits((float)(season * day));

                Assert.Equal(expected, BitConverter.SingleToInt32Bits(model.DayFactorAt(t)));
                Assert.Equal(expected, BitConverter.SingleToInt32Bits(ramped.DayFactorAt(t)));
            }
        }

        // ------------------------------------------------------------------ the two seasons

        [Fact]
        public void ARunOpensInSummerAndRound57sFirstWinterRunsFrom2750To5750()
        {
            LightModel sharp = Round57(ramp: 0f);

            Assert.Equal(1.0, sharp.TwoSeasonSign(0.0));
            Assert.Equal(1.0, sharp.TwoSeasonSign(2749.0));
            Assert.Equal(-1.0, sharp.TwoSeasonSign(2751.0));
            Assert.Equal(-1.0, sharp.TwoSeasonSign(5749.0));
            Assert.Equal(1.0, sharp.TwoSeasonSign(5751.0));
            Assert.Equal(-1.0, sharp.TwoSeasonSign(2750.0 + 6000.0 * 7));

            // With the ramp, each switch is centred on its midnight and passes 0 there.
            LightModel ramped = Round57();
            Assert.Equal(0.0, ramped.TwoSeasonSign(2750.0), 12);
            Assert.Equal(0.0, ramped.TwoSeasonSign(5750.0), 12);
            Assert.Equal(1.0, ramped.TwoSeasonSign(2750.0 - 125.0));
            Assert.Equal(-1.0, ramped.TwoSeasonSign(2750.0 + 125.0));
            Assert.True(ramped.TwoSeasonSign(2700.0) > 0.0 && ramped.TwoSeasonSign(2700.0) < 1.0);
            Assert.True(ramped.TwoSeasonSign(2800.0) < 0.0 && ramped.TwoSeasonSign(2800.0) > -1.0);

            // Odd about the switch.
            for (double x = 1.0; x < 125.0; x += 7.0)
            {
                Assert.Equal(-ramped.TwoSeasonSign(2750.0 - x), ramped.TwoSeasonSign(2750.0 + x), 12);
                Assert.Equal(-ramped.TwoSeasonSign(5750.0 - x), ramped.TwoSeasonSign(5750.0 + x), 12);
            }

            // Each switch sits at a midnight, the darkest point of the day sine.
            var dayOnly = new LightModel(48f, 12f) { DayNightAmplitude = 0.5f, DayLengthSeconds = 1000f };
            Assert.Equal(0.5f, dayOnly.DayFactorAt(2750.0), 6);
            Assert.Equal(0.5f, dayOnly.DayFactorAt(5750.0), 6);
        }

        [Theory]
        [InlineData(250f, 0.5f)]
        [InlineData(0f, 0.5f)]
        [InlineData(250f, 0f)]
        [InlineData(1000f, 0.5f)]
        public void TheTwoSeasonYearsMeanIsOneAndEachHalfIsOnePlusOrMinusItsDepth(float ramp, float day)
        {
            LightModel model = Round57(ramp, day);

            const double Year = 6000.0;
            const int Samples = 600_000;
            double h = Year / Samples;

            double sum = 0.0, summer = 0.0, winter = 0.0;
            int summerCount = 0, winterCount = 0;

            for (int i = 0; i < Samples; i++)
            {
                // Midpoints, from the year's first midnight (−D/4) so each half is whole days.
                double t = -250.0 + (i + 0.5) * h;
                double f = model.DayFactorAt(t);
                sum += f;

                if (t < 2750.0) { summer += f; summerCount++; }
                else { winter += f; winterCount++; }
            }

            double mean = sum / Samples;
            double summerMean = summer / summerCount;
            double winterMean = winter / winterCount;

            _output.WriteLine(
                $"ramp {ramp} s, day {day}: year {mean:R}, summer {summerMean:R}, winter {winterMean:R}");

            Assert.True(Math.Abs(mean - 1.0) < 1e-6, $"the year's mean light is {mean:R}");

            // Give or take the ramps: at most a ramp's length of each half is off its level.
            double slack = 0.6 * ramp / 3000.0 + 1e-6;
            Assert.True(Math.Abs(summerMean - 1.6) <= slack, $"summer's mean is {summerMean:R}");
            Assert.True(Math.Abs(winterMean - 0.4) <= slack, $"winter's mean is {winterMean:R}");
            Assert.Equal(summerMean - 1.0, 1.0 - winterMean, 6);
        }

        // ------------------------------------------------------------------ the refusals

        private static RunConfig Staged(LightModel light) => new RunConfig
        {
            MinimumPopulation = 12,
            MaximumPopulation = 2_000,
            WorldAreaSquareMetres = 100f,
            WorldDepthMetres = 20f,
            Light = light,
        };

        [Fact]
        public void TheWorldRefusesWhatTheTwoSeasonsAndTheWeatherCannotDescribe()
        {
            // Round 57's settings are accepted, weather and all.
            Assert.NotNull(new World(Staged(Round57(spread: 0.3f)), seed: 1));

            // An odd number of days: 5,000 s over 1,000 s, the year the owner first asked for.
            LightModel odd = Round57();
            odd.SeasonLengthSeconds = 5000f;
            ArgumentException e = Assert.Throws<ArgumentException>(() => new World(Staged(odd), seed: 1));
            _output.WriteLine(e.Message);
            Assert.Contains("even number of days", e.Message);

            // The smooth shape takes the same year, which is a whole number of days.
            odd.SeasonShape = SeasonShape.Smooth;
            Assert.NotNull(new World(Staged(odd), seed: 1));

            // Without a day cycle the year need not be days at all.
            LightModel noDay = Round57(day: 0f);
            noDay.SeasonLengthSeconds = 5000f;
            Assert.NotNull(new World(Staged(noDay), seed: 1));

            // A ramp of half a season (a quarter of the year) or more.
            e = Assert.Throws<ArgumentException>(() => new World(Staged(Round57(ramp: 1500f)), seed: 1));
            _output.WriteLine(e.Message);
            Assert.Contains("half a season", e.Message);
            Assert.NotNull(new World(Staged(Round57(ramp: 1499f)), seed: 1));

            // Weather without a day cycle.
            e = Assert.Throws<ArgumentException>(
                () => new World(Staged(Round57(day: 0f, spread: 0.3f)), seed: 1));
            _output.WriteLine(e.Message);
            Assert.Contains("day cycle", e.Message);

            // And the setters' own: a correlation of 1 or below 0, a negative spread or ramp.
            var model = new LightModel();
            ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(
                () => model.WeatherCorrelation = 1f);
            _output.WriteLine(r.Message);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.WeatherCorrelation = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.WeatherCorrelation = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.WeatherSpread = -0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.WeatherSpread = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.WeatherSpread = float.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SeasonRampSeconds = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SeasonRampSeconds = float.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => model.SeasonShape = (SeasonShape)7);
        }

        // ------------------------------------------------------------------ the weather

        [Fact]
        public void TheSameSeedGivesTheSameSkyInAnyCallOrder()
        {
            LightModel forward = Round57(spread: 0.3f);
            LightModel backward = Round57(spread: 0.3f);
            LightModel scattered = Round57(spread: 0.3f);

            var a = new double[400];
            for (int n = 0; n < a.Length; n++) a[n] = forward.WeatherLogFactorOfDay(n);

            for (int n = a.Length - 1; n >= 0; n--)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(a[n]),
                    BitConverter.DoubleToInt64Bits(backward.WeatherLogFactorOfDay(n)));
            }

            // Through the clock as well, out of order, against the forward model's.
            for (int i = 0; i < 2_000; i++)
            {
                double t = ((i * 7919L) % 2_000) * 173.3;
                Assert.Equal(BitConverter.SingleToInt32Bits(forward.DayFactorAt(t)),
                    BitConverter.SingleToInt32Bits(scattered.DayFactorAt(t)));
            }

            // Another seed is another sky.
            LightModel other = Round57(spread: 0.3f);
            other.WeatherSeed = 2UL;
            Assert.NotEqual(a[3], other.WeatherLogFactorOfDay(3));

            // The cache follows the config: change the seed back and the first sky returns.
            other.WeatherSeed = 1UL;
            Assert.Equal(a[3], other.WeatherLogFactorOfDay(3));
        }

        [Fact]
        public void AtASpreadOfZeroTheWeatherIsExactlyOne()
        {
            LightModel model = Round57(spread: 0f);
            LightModel twin = Round57(spread: 0f);
            twin.WeatherSeed = 99UL;

            for (int i = 0; i < 20_000; i++)
            {
                double t = i * 0.73;
                Assert.Equal(1.0, model.WeatherFactorAt(t));
                Assert.Equal(model.DayFactorAt(t), twin.DayFactorAt(t));
            }

            Assert.Equal(1.0, model.WeatherFactorOfDay(12));
        }

        [Fact]
        public void ADaysFactorHoldsFromItsDawnToItsDuskAndBlendsAcrossMidnight()
        {
            LightModel model = Round57(spread: 0.3f);

            for (int n = 1; n < 30; n++)
            {
                double midnight = n * 1000.0 - 250.0;
                double here = model.WeatherFactorOfDay(n - 1);
                double next = model.WeatherFactorOfDay(n);

                // Outside the quarter-day round each midnight a day is its own factor.
                Assert.Equal(here, model.WeatherFactorAt(midnight - 500.0));
                Assert.Equal(here, model.WeatherFactorAt(midnight - 125.0));
                Assert.Equal(next, model.WeatherFactorAt(midnight + 125.0));

                // At the midnight, halfway; and on the way, between the two.
                Assert.Equal(0.5 * (here + next), model.WeatherFactorAt(midnight), 12);
                double partway = model.WeatherFactorAt(midnight + 60.0);
                Assert.InRange(partway, Math.Min(here, next), Math.Max(here, next));

                // The light is the product of the three.
                double t = midnight + 300.0;
                double season = 1.0 + 0.6f * model.TwoSeasonSign(t);
                double day = 1.0 + 0.5f * Math.Sin(2.0 * Math.PI * t / 1000f);
                Assert.Equal((float)(season * day * model.WeatherFactorAt(t)), model.DayFactorAt(t));
            }
        }

        [Fact]
        public void OverAHundredThousandDaysTheMeanFactorIsOneAndTheLogsCorrelationIsRho()
        {
            LightModel model = Round57(spread: 0.3f);
            const int Days = 100_000;
            const double Sigma = 0.3, Rho = 0.5;

            double sumFactor = 0.0, sumLog = 0.0;
            var x = new double[Days];
            for (int n = 0; n < Days; n++)
            {
                x[n] = model.WeatherLogFactorOfDay(n);
                sumLog += x[n];
                sumFactor += Math.Exp(x[n]);
            }

            double meanFactor = sumFactor / Days;
            double meanLog = sumLog / Days;

            double c0 = 0.0, c1 = 0.0;
            for (int n = 0; n < Days; n++)
            {
                double d = x[n] - meanLog;
                c0 += d * d;
                if (n + 1 < Days) c1 += d * (x[n + 1] - meanLog);
            }

            double lag1 = c1 / c0;
            double spread = Math.Sqrt(c0 / Days);

            // Sampling errors: the factor's variance is e^{σ²} − 1, inflated by (1 + ρ)/(1 − ρ) for
            // the correlation; the lag-1 estimate's is (1 − ρ²)/n. Each asserted to four of them.
            double factorError = Math.Sqrt((Math.Exp(Sigma * Sigma) - 1.0) / Days * (1 + Rho) / (1 - Rho));
            double lagError = Math.Sqrt((1 - Rho * Rho) / Days);

            _output.WriteLine(
                $"mean factor {meanFactor:R} (±{factorError:0.#####}), mean log {meanLog:R} " +
                $"(μ {-0.5 * Sigma * Sigma}), log sd {spread:R}, lag-1 {lag1:R} (±{lagError:0.#####})");

            Assert.True(Math.Abs(meanFactor - 1.0) < 4 * factorError, $"mean factor {meanFactor:R}");
            Assert.True(Math.Abs(lag1 - Rho) < 4 * lagError, $"lag-1 correlation {lag1:R}");
            Assert.True(Math.Abs(spread - Sigma) < 0.01, $"the log's spread is {spread:R}");
        }
    }
}
