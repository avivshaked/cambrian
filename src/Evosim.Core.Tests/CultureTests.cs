using System;
using System.Globalization;
using System.Threading;
using Evosim.Core;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// Every string this project writes for a machine to read must be culture-independent —
    /// the Sol/GPT review of 2026-09-03, finding 7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The failure is invisible on this machine and total on another.</b> An interpolated
    /// <c>$"{x:0.###}"</c> formats with the process culture, so a run launched on a machine
    /// configured for a comma decimal writes <c>vent 0,1 m/s</c> into its header, <c>0,1</c>
    /// into any log line built the same way, and fails
    /// <c>VentTests.TheVentKnobsRefuseNonsenseAtTheSetter</c> for a reason that has nothing to
    /// do with the vent. Nothing about the world changed; only the text describing it did, and
    /// the text is the record.
    /// </para>
    /// <para>
    /// <b>de-DE, and not merely "some other culture".</b> It swaps the decimal point and the
    /// group separator both, which is the exact pair that turns one number into another number
    /// rather than into something obviously broken — <c>1.234</c> read back as 1234.
    /// </para>
    /// <para>
    /// The culture is restored in a <c>finally</c>: xunit reuses threads across collections, so
    /// a test that leaves the culture changed hands its failure to whichever test runs next on
    /// that thread, which is the worst possible place for it to surface.
    /// </para>
    /// </remarks>
    public class CultureTests
    {
        private static void InGerman(Action body)
        {
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUiCulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                var german = new CultureInfo("de-DE");
                Thread.CurrentThread.CurrentCulture = german;
                Thread.CurrentThread.CurrentUICulture = german;

                // The guard on the guard: if the runtime is running in globalisation-invariant
                // mode, "de-DE" resolves to a culture that formats like the invariant one and
                // every assertion below would pass without testing anything.
                Assert.Equal(",", german.NumberFormat.NumberDecimalSeparator);

                body();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
                Thread.CurrentThread.CurrentUICulture = savedUiCulture;
            }
        }

        [Fact]
        public void TheCurrentFieldDescribesItselfWithADecimalPointInAnyCulture()
        {
            InGerman(() =>
            {
                var field = new CurrentField
                {
                    Speed = 0.25f,
                    CellMetres = 12.5f,
                    PeriodSeconds = 600f,
                    VentSpeed = 0.1f,
                    VentPatch = 2,
                };

                string text = field.ToString();

                Assert.Contains("0.25 m/s peak", text);
                Assert.Contains("12.5 m cells", text);
                Assert.Contains("vent 0.1 m/s in patch 2", text);
                Assert.DoesNotContain(",", text.Replace(", ", ""));
            });
        }

        [Fact]
        public void TheHeaderFacingDescriptionsOfEveryTunableUseADecimalPointInAnyCulture()
        {
            // The config's own text, which reaches config.json's comments, any editor, and —
            // through Format() — the config hash itself. A hash that changes with the operating
            // system's regional settings would break §7's promise that (genome, seed, configHash)
            // identifies a run, in the least visible direction there is.
            var config = new RunConfig();
            string invariantHash = config.Hash();

            InGerman(() =>
            {
                Assert.Equal(invariantHash, new RunConfig().Hash());

                foreach (TunableEntry entry in ConfigSchema.Of(new RunConfig()))
                {
                    Assert.DoesNotContain(",", entry.Format());

                    // TunableEntry.ToString is the one-line description an editor or a header
                    // would print. It must carry the same text Format() produced, not a second,
                    // culture-formatted rendering of the same number.
                    Assert.Contains(entry.Format(), entry.ToString());
                }
            });
        }

        [Fact]
        public void TheModelsThatDescribeThemselvesForTheRecordUseADecimalPointInAnyCulture()
        {
            InGerman(() =>
            {
                Assert.Contains("1.5", new LightModel(120f, 1.5f).ToString());
                Assert.Contains("0.125", new FluidConfig { DragCoefficient = 0.125f }.ToString());
                var brood = new ReproductionTraits { BroodSize = 1, OffspringEndowment = 0.25f };
                Assert.Contains("0.3", brood.ToString());   // "0.#" rounds; the point is the point

                var limits = new DevelopmentLimits { MinPartHalfExtent = 0.125f };
                Assert.Contains("0.125", limits.ToString());
            });
        }

        [Fact]
        public void GenomeJsonRoundTripsThroughAGermanCulture()
        {
            // Json.Writer is already invariant by construction; this asserts it stays that way,
            // because a genome written with comma decimals is not merely ugly — it is a different
            // creature wearing the original's identity, and nothing downstream can notice.
            Genome genome = Fixtures.SelfLoopSpine(recursiveLimit: 2, segmentScale: 0.75f);
            string invariantText = GenomeJson.Write(genome);

            InGerman(() =>
            {
                string germanText = GenomeJson.Write(genome);
                Assert.Equal(invariantText, germanText);

                // And it reads back, which is the half a writer-only assertion cannot see.
                Assert.Equal(genome.Nodes.Count, GenomeJson.Read(germanText).Nodes.Count);
            });
        }
    }
}
