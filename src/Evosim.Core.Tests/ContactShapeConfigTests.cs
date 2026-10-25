using System;
using Xunit;

namespace Evosim.Core.Tests
{
    /// <summary>
    /// D228's switch as a tunable: it defaults to the recorded spheres, moves the hash, survives a save and a
    /// reload by name, and is refused without per-part contact.
    /// </summary>
    public sealed class ContactShapeConfigTests
    {
        [Fact]
        public void ItDefaultsToTheSpheresMovesTheHashAndSurvivesASaveAndReloadByName()
        {
            var off = new RunConfig { ContactPerPart = true };
            Assert.Equal(ContactShapeForm.Spheres, off.ContactShape);

            var on = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.TrueShape };
            Assert.NotEqual(off.Hash(), on.Hash());

            string text = RunConfigJson.Write(on);
            Assert.Contains("\"contactShape\": \"TrueShape\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.Equal(ContactShapeForm.TrueShape, back.ContactShape);
        }

        [Fact]
        public void TheThirdValueIsDistinctSurvivesASaveAndReloadByNameAndNeedsPerPartContactToo()
        {
            var shape = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.TrueShape };
            var point = new RunConfig { ContactPerPart = true, ContactShape = ContactShapeForm.TrueShapeAtPoint };
            Assert.NotEqual(shape.Hash(), point.Hash());

            string text = RunConfigJson.Write(point);
            Assert.Contains("\"contactShape\": \"TrueShapeAtPoint\"", text);

            RunConfig back = RunConfigJson.Read(text, out string mismatch);
            Assert.True(mismatch == null, mismatch);
            Assert.Equal(ContactShapeForm.TrueShapeAtPoint, back.ContactShape);

            // Appended after the second value, so the first two keep their numbers and their names.
            Assert.Equal(0, (int)ContactShapeForm.Spheres);
            Assert.Equal(1, (int)ContactShapeForm.TrueShape);
            Assert.Equal(2, (int)ContactShapeForm.TrueShapeAtPoint);

            var e = Assert.Throws<ArgumentException>(
                () => new World(new RunConfig { ContactShape = ContactShapeForm.TrueShapeAtPoint }, seed: 1));
            Assert.Contains("ContactShape", e.Message);
            Assert.Contains("ContactPerPart", e.Message);
            Assert.NotNull(new World(point, seed: 1));
        }

        [Fact]
        public void AWorldRefusesTheTrueShapeWithoutPerPartContactAndNamesBoth()
        {
            var e = Assert.Throws<ArgumentException>(
                () => new World(new RunConfig { ContactShape = ContactShapeForm.TrueShape }, seed: 1));
            Assert.Contains("ContactShape", e.Message);
            Assert.Contains("ContactPerPart", e.Message);

            // With per-part contact it builds.
            var world = new World(
                new RunConfig { ContactShape = ContactShapeForm.TrueShape, ContactPerPart = true }, seed: 1);
            Assert.NotNull(world);
        }
    }
}
