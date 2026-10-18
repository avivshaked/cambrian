using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Evosim.Core;

namespace Evosim.Nursery
{
    /// <summary>
    /// One body's measures from one episode: what a reward may read. A reward never sees the world,
    /// only these rows, so a score can be recomputed from the episodes file alone, and a new reward
    /// is a new function of numbers that already exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The row is the file's format.</b> <see cref="ToJson"/> and <see cref="FromJson"/> are the
    /// one place that knows how an episode is written, and <see cref="FromJson"/> refuses a missing
    /// or an unknown field, as every loader here does: a row that loads with a field defaulted is
    /// scored as an episode it was not.
    /// </para>
    /// <para>
    /// <b>The seed is a string.</b> A JSON reader holds every number as a double, which keeps 53 bits
    /// of a 64-bit seed. It is written as <c>"0x"</c> and lowercase hex, and read back whole.
    /// </para>
    /// </remarks>
    public sealed class EpisodeRecord
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The search generation the episode was swum in; the exam uses 0.</summary>
        public int Generation;
        public int Draw;
        public ulong Seed;

        /// <summary><c>search</c> or <c>exam</c>.</summary>
        public string Arena;

        public int Body;

        /// <summary>The episode's length, in seconds.</summary>
        public double Seconds;

        /// <summary>Holdings at the start, in joules.</summary>
        public double Start;

        /// <summary>Holdings at the end, in joules; 0 for a body that died.</summary>
        public double End;

        /// <summary><see cref="End"/> minus <see cref="Start"/>.</summary>
        public double Net;

        /// <summary>Joules over the episode: eaten, from light, spent on work, on upkeep and on handling, and exuded.</summary>
        public double Food, Light, Work, Upkeep, Handling, Exuded;

        /// <summary>
        /// Joules the body's mouths kept over the episode, net of the waste returned to the water: its
        /// <c>Organism.MouthJoules</c> at the end less at the start. A body that died keeps what it ate
        /// before it died. <see cref="Food"/> is the ledger's income from the water and never holds a meal.
        /// </summary>
        public double Eaten;

        /// <summary>
        /// The kills the body's bites made: each body or part a world's kill row names it the attacker of (a whole leaf, or a
        /// limb), counted while it lives. With <see cref="Killed"/>, the joules those kills held, and <see cref="Meals"/>, the
        /// economy steps in which its mouths kept food, they say whether a body stays with what it kills.
        /// </summary>
        public double Kills, Killed, Meals;

        /// <summary>Distance travelled, in metres.</summary>
        public double Path;

        /// <summary>The rocking: the root's mean angular speed over the steps it was alive, rad/s; NaN when no step was measured.</summary>
        public double Turn;

        /// <summary>Mean food density at the body, in J/m3; NaN when no step was measured.</summary>
        public double Fresh;

        public bool Survived;

        /// <summary>Seconds into the episode at death; NaN when the body survived.</summary>
        public double DiedAt;

        /// <summary>
        /// The names a reward may read, in this fixed order; a reward that sums over measures sums in
        /// this order so that its rounding never depends on how it was configured. <c>survived</c>
        /// reads as 1 or 0.
        /// </summary>
        public static readonly IReadOnlyList<string> Measures = new[]
        {
            "start", "end", "net", "food", "eaten", "kills", "killed", "meals", "light", "work", "upkeep", "handling", "exuded",
            "path", "turn", "fresh", "survived", "diedAt",
        };

        /// <summary>The value of one named measure; an unknown name throws, never reads as zero.</summary>
        public double Measure(string name)
        {
            switch (name)
            {
                case "start": return Start;
                case "end": return End;
                case "net": return Net;
                case "food": return Food;
                case "eaten": return Eaten;
                case "kills": return Kills;
                case "killed": return Killed;
                case "meals": return Meals;
                case "light": return Light;
                case "work": return Work;
                case "upkeep": return Upkeep;
                case "handling": return Handling;
                case "exuded": return Exuded;
                case "path": return Path;
                case "turn": return Turn;
                case "fresh": return Fresh;
                case "survived": return Survived ? 1d : 0d;
                case "diedAt": return DiedAt;
                default:
                    throw new ArgumentException(
                        "no measure named '" + name + "'; the measures are " + string.Join(", ", Measures) + ".",
                        nameof(name));
            }
        }

        private static readonly string[] FieldNames =
        {
            "generation", "draw", "seed", "arena", "body", "seconds", "start", "end", "net", "food", "eaten", "kills", "killed", "meals",
            "light", "work", "upkeep", "handling", "exuded", "path", "turn", "fresh", "survived", "diedAt",
        };

        /// <summary>The row, on one line. Doubles by "R"; NaN as null. An infinite value throws, as the writer does.</summary>
        public string ToJson()
        {
            if (Arena != "search" && Arena != "exam")
                throw new InvalidOperationException("an episode row's arena is search or exam, not " + (Arena ?? "null"));
            var w = new Json.Writer(indent: false);
            w.BeginObject();
            w.Field("generation", Generation);
            w.Field("draw", Draw);
            w.Field("seed", "0x" + Seed.ToString("x", Inv));
            w.Field("arena", Arena);
            w.Field("body", Body);
            Number(w, "seconds", Seconds);
            Number(w, "start", Start);
            Number(w, "end", End);
            Number(w, "net", Net);
            Number(w, "food", Food);
            Number(w, "eaten", Eaten);
            Number(w, "kills", Kills);
            Number(w, "killed", Killed);
            Number(w, "meals", Meals);
            Number(w, "light", Light);
            Number(w, "work", Work);
            Number(w, "upkeep", Upkeep);
            Number(w, "handling", Handling);
            Number(w, "exuded", Exuded);
            Number(w, "path", Path);
            Number(w, "turn", Turn);
            Number(w, "fresh", Fresh);
            w.Field("survived", Survived);
            Number(w, "diedAt", DiedAt);
            w.EndObject();
            return w.ToString();
        }

        private static void Number(Json.Writer w, string name, double value)
        {
            if (double.IsNaN(value)) w.Raw(name, "null"); else w.Field(name, value);
        }

        /// <summary>The inverse of <see cref="ToJson"/>. Throws <see cref="InvalidDataException"/> naming the field that is missing, unknown or of the wrong kind.</summary>
        public static EpisodeRecord FromJson(string line)
        {
            JsonNode node;
            try { node = Json.Parse(line); }
            catch (FormatException e) { throw new InvalidDataException("an episode row is not JSON: " + e.Message, e); }
            if (node.Kind != JsonNode.NodeKind.Object) throw new InvalidDataException("an episode row is not a JSON object");

            foreach (string key in node.Keys())
                if (Array.IndexOf(FieldNames, key) < 0)
                    throw new InvalidDataException("an episode row has an unknown field '" + key + "'");
            foreach (string name in FieldNames)
                if (!node.Has(name))
                    throw new InvalidDataException("an episode row lacks the field '" + name + "'");

            return new EpisodeRecord
            {
                Generation = Read(node, "generation", Whole),
                Draw = Read(node, "draw", Whole),
                Seed = Read(node, "seed", ReadSeed),
                Arena = Read(node, "arena", n => n.Kind == JsonNode.NodeKind.String && (n.AsString() == "search" || n.AsString() == "exam")
                    ? n.AsString() : throw new FormatException("expected \"search\" or \"exam\"")),
                Body = Read(node, "body", Whole),
                Seconds = ReadDouble(node, "seconds"),
                Start = ReadDouble(node, "start"),
                End = ReadDouble(node, "end"),
                Net = ReadDouble(node, "net"),
                Food = ReadDouble(node, "food"),
                Eaten = ReadDouble(node, "eaten"),
                Kills = ReadDouble(node, "kills"),
                Killed = ReadDouble(node, "killed"),
                Meals = ReadDouble(node, "meals"),
                Light = ReadDouble(node, "light"),
                Work = ReadDouble(node, "work"),
                Upkeep = ReadDouble(node, "upkeep"),
                Handling = ReadDouble(node, "handling"),
                Exuded = ReadDouble(node, "exuded"),
                Path = ReadDouble(node, "path"),
                Turn = ReadDouble(node, "turn"),
                Fresh = ReadDouble(node, "fresh"),
                Survived = Read(node, "survived", n => n.AsBool()),
                DiedAt = ReadDouble(node, "diedAt"),
            };
        }

        private static double ReadDouble(JsonNode node, string name) =>
            Read(node, name, n =>
            {
                if (n.Kind == JsonNode.NodeKind.Null) return double.NaN;
                double d = n.AsDouble();
                // The writer refuses an infinity, so a row that holds one was not written by it.
                return double.IsInfinity(d) ? throw new FormatException("an infinite value, which no row is written with") : d;
            });

        private static int Whole(JsonNode n)
        {
            double d = n.AsDouble();
            if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw new FormatException("expected a whole number, found " + d.ToString("R", Inv));
            return (int)d;
        }

        private static ulong ReadSeed(JsonNode n)
        {
            string s = n.Kind == JsonNode.NodeKind.String ? n.AsString() : throw new FormatException("expected a hex string");
            if (!s.StartsWith("0x", StringComparison.Ordinal) ||
                !ulong.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, Inv, out ulong seed))
                throw new FormatException("expected \"0x\" and hex digits, found \"" + s + "\"");
            return seed;
        }

        private static T Read<T>(JsonNode node, string name, Func<JsonNode, T> read)
        {
            try { return read(node[name]); }
            catch (Exception e) when (e is FormatException || e is OverflowException)
            {
                throw new InvalidDataException("the episode field '" + name + "' is unreadable: " + e.Message, e);
            }
        }
    }

    /// <summary>
    /// What a reward returns for one genome: the number selection ranks by, and the named parts
    /// that say why it scored as it did.
    /// </summary>
    public sealed class Score
    {
        /// <summary>The number selection ranks by; NaN when there were no episodes, or a measure it sums was NaN.</summary>
        public double Value { get; }

        /// <summary>Named parts in a fixed order, for a report. Never null; may be empty.</summary>
        public IReadOnlyList<KeyValuePair<string, double>> Parts { get; }

        public Score(double value, IReadOnlyList<KeyValuePair<string, double>> parts)
        {
            Value = value;
            Parts = parts ?? Array.Empty<KeyValuePair<string, double>>();
        }
    }

    /// <summary>A scoring rule: one genome's episodes in, one <see cref="Score"/> out.</summary>
    public interface IReward
    {
        /// <summary>
        /// The score of one genome, from all of its episodes, every generation it has swum, in the
        /// order they were swum.
        /// </summary>
        Score Score(IReadOnlyList<EpisodeRecord> episodes);

        /// <summary>The measures this reward reads, names from <see cref="EpisodeRecord.Measures"/>.</summary>
        IReadOnlyList<string> Reads { get; }
    }

    /// <summary>
    /// A reward that can refuse its own parameters. <see cref="RewardRegistry.Create"/> calls
    /// <see cref="Validate"/> once every parameter is set, so that a bad recipe stops before the
    /// first episode, not at the first score.
    /// </summary>
    public interface IValidatedReward
    {
        /// <summary>Throws when the parameters make no sense together.</summary>
        void Validate();
    }

    /// <summary>
    /// How a genome's episodes combine into one number.
    /// </summary>
    public static class RewardMath
    {
        /// <summary>
        /// The mean of <paramref name="value"/> over the episodes, kept as the tank search kept it:
        /// one search generation at a time, each generation's sum folded into the running mean. NaN
        /// for no episodes.
        /// </summary>
        /// <remarks>
        /// <b>This is the loop that ran before rewards existed, and it is kept to the bit.</b> The
        /// tank held a running mean per genome and updated it after each generation; the baseline
        /// check compares an old run's rows with the new code's bit for bit. The weighted form
        /// equals the plain mean on paper and not in floating point. It stays a sequential sum from 0,
        /// left to right in draw order, then the update: the old code's <c>Enumerable.Sum</c> with a
        /// selector is that loop in .NET 8, but its order is not promised, and <c>Average</c>, a
        /// pairwise or a Kahan sum, or a reordering would each round differently. A different last bit
        /// would read as a changed run. So the episodes must come in the order swum: one arena, the
        /// generations never going back, and the draws rising within a generation; anything else is
        /// refused rather than summed in a new order. One thing differs
        /// from the old loop, which took a NaN score to mean "no generation yet": here the first
        /// generation is the one with no episodes counted before it. On finite values the two are the
        /// same to the bit (the tank's net is always finite). On a NaN they part: the old loop forgot
        /// a NaN generation once a clean one followed, and this one keeps the mean NaN from then on,
        /// so a reward that reads a missing measure cannot pass for a clean score.
        /// </remarks>
        public static double RunningMean(IReadOnlyList<EpisodeRecord> episodes, Func<EpisodeRecord, double> value)
        {
            double score = double.NaN;
            int count = 0;
            int i = 0;
            for (int k = 1; k < episodes.Count; k++)
            {
                EpisodeRecord a = episodes[k - 1], b = episodes[k];
                if (b.Arena != a.Arena || b.Generation < a.Generation || (b.Generation == a.Generation && b.Draw <= a.Draw))
                    throw new ArgumentException(
                        $"episodes come in the order swum: one arena, generations never going back, draws rising within one; episode {k} is {b.Arena} generation {b.Generation} draw {b.Draw} after {a.Arena} generation {a.Generation} draw {a.Draw}");
            }
            while (i < episodes.Count)
            {
                int generation = episodes[i].Generation;
                double sum = 0;
                int n = 0;
                while (i < episodes.Count && episodes[i].Generation == generation)
                {
                    sum += value(episodes[i]);
                    n++;
                    i++;
                }
                score = count == 0 ? sum / n : (score * count + sum) / (count + n);
                count += n;
            }
            return score;
        }
    }

    /// <summary>Marks a class as a reward and names it, as <c>[Tunable]</c> marks a config setting.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RewardAttribute : Attribute
    {
        /// <summary>The name a recipe uses.</summary>
        public string Name { get; }

        public RewardAttribute(string name) { Name = name; }
    }

    /// <summary>
    /// Marks a public settable property of a reward as a parameter a recipe supplies. Allowed types
    /// are double, int, bool, string and <c>Dictionary&lt;string, double&gt;</c>. The recipe names it
    /// by the property's name with a lowercase first letter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RewardParameterAttribute : Attribute
    {
        /// <summary>One line on what this decides.</summary>
        public string Description { get; }

        public RewardParameterAttribute(string description) { Description = description; }
    }

    /// <summary>
    /// Every reward the assembly holds, found by its <see cref="RewardAttribute"/>, and the one place
    /// that builds one from a recipe's parameters.
    /// </summary>
    /// <remarks>
    /// <b>Every parameter is required.</b> A default would be a number the recipe never said, and
    /// the run record would not hold it. A missing key, an unknown key and a value of the wrong kind
    /// all throw, naming the reward and the parameter.
    /// </remarks>
    public static class RewardRegistry
    {
        private static Dictionary<string, Type> _byName;

        private static Dictionary<string, Type> ByName()
        {
            if (_byName != null) return _byName;
            var found = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type t in typeof(RewardRegistry).Assembly.GetTypes())
            {
                var attr = t.GetCustomAttribute<RewardAttribute>(inherit: false);
                if (attr == null || !t.IsClass || t.IsAbstract || !typeof(IReward).IsAssignableFrom(t)) continue;
                if (found.ContainsKey(attr.Name))
                    throw new InvalidOperationException(
                        "two rewards are named '" + attr.Name + "': " + found[attr.Name].Name + " and " + t.Name);
                found[attr.Name] = t;
            }
            _byName = found;
            return found;
        }

        /// <summary>Every reward's name, sorted ordinal.</summary>
        public static IReadOnlyList<string> Names =>
            ByName().Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Builds the reward called <paramref name="name"/> and sets its parameters from the JSON
        /// object <paramref name="parameters"/>. Parameters are applied in the order of the property
        /// names, sorted ordinal, so that nothing depends on reflection order.
        /// </summary>
        public static IReward Create(string name, JsonNode parameters)
        {
            if (name == null || !ByName().TryGetValue(name, out Type type))
                throw new ArgumentException(
                    "no reward named '" + name + "'; the rewards are " + string.Join(", ", Names) + ".", nameof(name));
            if (parameters == null || parameters.Kind != JsonNode.NodeKind.Object)
                throw new ArgumentException("the parameters of reward '" + name + "' are not a JSON object", nameof(parameters));

            var reward = (IReward)Activator.CreateInstance(type);
            PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<RewardParameterAttribute>() != null)
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToArray();
            var keys = props.Select(KeyOf).ToArray();

            foreach (string key in parameters.Keys())
                if (Array.IndexOf(keys, key) < 0)
                    throw new ArgumentException(
                        "reward '" + name + "' has no parameter '" + key + "'; its parameters are " +
                        (keys.Length == 0 ? "none" : string.Join(", ", keys)) + ".");

            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanWrite || p.SetMethod == null || !p.SetMethod.IsPublic)
                    throw new InvalidOperationException("the parameter '" + keys[i] + "' of reward '" + name + "' has no public setter");
                if (!parameters.Has(keys[i]))
                    throw new ArgumentException("reward '" + name + "' lacks its parameter '" + keys[i] + "'; every parameter is required.");
                p.SetValue(reward, Convert(name, keys[i], p.PropertyType, parameters[keys[i]]));
            }

            foreach (string read in reward.Reads)
                if (Array.IndexOf(EpisodeRecord.Measures.ToArray(), read) < 0)
                    throw new InvalidOperationException(
                        "reward '" + name + "' reads '" + read + "', which is not a measure; the measures are " +
                        string.Join(", ", EpisodeRecord.Measures) + ".");

            (reward as IValidatedReward)?.Validate();
            return reward;
        }

        private static string KeyOf(PropertyInfo p) => char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);

        private static object Convert(string reward, string key, Type type, JsonNode node)
        {
            string at = "the parameter '" + key + "' of reward '" + reward + "'";
            if (type == typeof(double))
            {
                if (node.Kind != JsonNode.NodeKind.Number) throw new ArgumentException(at + " must be a number, found " + node.Kind);
                return node.AsDouble();
            }
            if (type == typeof(int))
            {
                if (node.Kind != JsonNode.NodeKind.Number) throw new ArgumentException(at + " must be a number, found " + node.Kind);
                double d = node.AsDouble();
                if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue)
                    throw new ArgumentException(at + " must be a whole number in the range of an int, found " + node);
                return (int)d;
            }
            if (type == typeof(bool))
            {
                if (node.Kind != JsonNode.NodeKind.Bool) throw new ArgumentException(at + " must be true or false, found " + node.Kind);
                return node.AsBool();
            }
            if (type == typeof(string))
            {
                if (node.Kind != JsonNode.NodeKind.String) throw new ArgumentException(at + " must be a string, found " + node.Kind);
                return node.AsString();
            }
            if (type == typeof(Dictionary<string, double>))
            {
                if (node.Kind != JsonNode.NodeKind.Object) throw new ArgumentException(at + " must be an object of named numbers, found " + node.Kind);
                var map = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (string k in node.Keys())
                {
                    JsonNode v = node[k];
                    if (v.Kind != JsonNode.NodeKind.Number) throw new ArgumentException(at + " holds '" + k + "', which must be a number, found " + v.Kind);
                    map[k] = v.AsDouble();
                }
                return map;
            }
            throw new InvalidOperationException(at + " has the type " + type.Name + ", which a reward parameter may not have");
        }
    }

    /// <summary>
    /// The joules a line holds at the end of an episode, less what it held at the start, kept as the
    /// tank's running mean. The reward the search ran on before rewards existed.
    /// </summary>
    [Reward("Net")]
    public sealed class Net : IReward
    {
        public IReadOnlyList<string> Reads { get; } = new[] { "net" };

        public Score Score(IReadOnlyList<EpisodeRecord> episodes)
        {
            double value = RewardMath.RunningMean(episodes, e => e.Net);
            return new Score(value, new[] { new KeyValuePair<string, double>("net", value) });
        }
    }

    /// <summary>
    /// D178: the energy a body nets, for a search that changes bodies: what its mouths kept (<c>eaten</c>), less its standing cost
    /// (<c>upkeep</c>: tissue, the attack and intake prices, buoyancy offset and support), its stroke's work (<c>work</c>) and the
    /// pumping its eating cost (<c>handling</c>), kept as the tank's running mean. An income score would pay for ever larger
    /// mouths once a body can grow them; this one charges for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dying must never pay.</b> A body stops paying upkeep when it dies, so one that eats less than it costs would score
    /// better the sooner it died. With <c>chargeDeathToEnd</c> true, a body that died is charged its standing cost to the
    /// episode's end, at the rate it paid while alive: upkeep times the episode's seconds over the seconds it lived. A body that
    /// died having paid no upkeep has no rate to carry on, and its episode is NaN, which ranks last. False charges what the ledger
    /// charged. The parameter is required, so the run record says which.
    /// </para>
    /// <para>
    /// The terms are summed in the order eaten, upkeep, work, handling, each episode's before the episodes combine by
    /// <see cref="RewardMath.RunningMean"/>. The parts are each term's own running mean, the costs with their sign, and sum to
    /// the value only up to rounding.
    /// </para>
    /// </remarks>
    [Reward("NetEnergy")]
    public sealed class NetEnergy : IReward
    {
        [RewardParameter("True charges a body that died its standing cost to the episode's end, so that dying never pays.")]
        public bool ChargeDeathToEnd { get; set; }

        public IReadOnlyList<string> Reads { get; } = new[] { "eaten", "work", "upkeep", "handling", "diedAt" };

        /// <summary>The standing cost the episode is charged: the ledger's, or carried on to the end for a body that died.</summary>
        public double Standing(EpisodeRecord e)
        {
            if (!ChargeDeathToEnd || double.IsNaN(e.DiedAt)) return e.Upkeep;
            if (!(e.Upkeep > 0) || !(e.DiedAt > 0)) return double.NaN;
            return e.Upkeep * (e.Seconds / e.DiedAt);
        }

        /// <summary>One episode's value.</summary>
        public double Of(EpisodeRecord e) => e.Eaten - Standing(e) - e.Work - e.Handling;

        public Score Score(IReadOnlyList<EpisodeRecord> episodes)
        {
            double value = RewardMath.RunningMean(episodes, Of);
            return new Score(value, new[]
            {
                new KeyValuePair<string, double>("eaten", RewardMath.RunningMean(episodes, e => e.Eaten)),
                new KeyValuePair<string, double>("upkeep", RewardMath.RunningMean(episodes, e => -Standing(e))),
                new KeyValuePair<string, double>("work", RewardMath.RunningMean(episodes, e => -e.Work)),
                new KeyValuePair<string, double>("handling", RewardMath.RunningMean(episodes, e => -e.Handling)),
            });
        }
    }

    /// <summary>
    /// The energy a body's eating returns on what it spends: what its mouths kept over its upkeep, work and handling, the
    /// costs <see cref="NetEnergy"/> charges, kept as the tank's running mean of each episode's ratio. A body breaks even
    /// at 1; one that eats nothing scores 0, however little it spends.
    /// </summary>
    /// <remarks>
    /// Under <see cref="NetEnergy"/> a body that cannot eat enough to pay for its stroke does best by not swimming at all,
    /// since idling loses least; the leaf tank's first body search found exactly that at D177's prices. A ratio cannot be
    /// raised by idling. The coordinator's argument for it (2026-10-14): in the world a founder's purse and its breeding gate
    /// scale with its standing cost, so a lineage's time to breed goes roughly as its costs over what it nets, and a score
    /// of eating over costs ranks bodies as the world's growth would. A ratio can favour a body that eats a crumb at almost
    /// no cost; a recipe guards against that by stopping on eaten and printing it beside the score.
    /// <c>chargeDeathToEnd</c> charges a dead body's upkeep as <see cref="NetEnergy.Standing"/> does. An episode whose
    /// costs are not above 0 has no ratio, and is NaN, which ranks last.
    /// </remarks>
    [Reward("EnergyReturn")]
    public sealed class EnergyReturn : IReward
    {
        [RewardParameter("True charges a body that died its standing cost to the episode's end, so that dying never pays.")]
        public bool ChargeDeathToEnd { get; set; }

        public IReadOnlyList<string> Reads { get; } = new[] { "eaten", "work", "upkeep", "handling", "diedAt" };

        private NetEnergy Costs => new NetEnergy { ChargeDeathToEnd = ChargeDeathToEnd };

        /// <summary>One episode's ratio.</summary>
        public double Of(EpisodeRecord e)
        {
            double spent = Costs.Standing(e) + e.Work + e.Handling;
            return spent > 0 ? e.Eaten / spent : double.NaN;
        }

        public Score Score(IReadOnlyList<EpisodeRecord> episodes)
        {
            NetEnergy costs = Costs;
            double value = RewardMath.RunningMean(episodes, Of);
            return new Score(value, new[]
            {
                new KeyValuePair<string, double>("eaten", RewardMath.RunningMean(episodes, e => e.Eaten)),
                new KeyValuePair<string, double>("spent", RewardMath.RunningMean(episodes, e => costs.Standing(e) + e.Work + e.Handling)),
            });
        }
    }

    /// <summary>
    /// A sum of measures, each times a weight: <c>weights</c> maps a measure name to its weight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per episode the sum is accumulated in <see cref="EpisodeRecord.Measures"/> order, never the
    /// dictionary's, and the episodes combine by <see cref="RewardMath.RunningMean"/>. The parts
    /// are each weighted measure's own running mean, in the same order; they sum to the value only
    /// up to rounding, because each is a mean taken apart.
    /// </para>
    /// <para>
    /// A measure that is NaN in an episode (<c>fresh</c> or <c>turn</c> with no step measured, <c>diedAt</c> in a
    /// body that lived) makes that episode's sum NaN, and so that generation's sum, and the running
    /// mean after it, NaN for good (<see cref="RewardMath.RunningMean"/>). That is not
    /// special-cased: a recipe that weights such a measure gets NaN scores, which rank last.
    /// </para>
    /// </remarks>
    [Reward("WeightedSum")]
    public sealed class WeightedSum : IReward, IValidatedReward
    {
        [RewardParameter("Measure name to weight; the score is the sum of weight times measure.")]
        public Dictionary<string, double> Weights { get; set; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>The weighted measures, in <see cref="EpisodeRecord.Measures"/> order.</summary>
        public IReadOnlyList<string> Reads =>
            EpisodeRecord.Measures.Where(m => Weights.ContainsKey(m)).ToList();

        public void Validate()
        {
            if (Weights == null || Weights.Count == 0)
                throw new ArgumentException("reward 'WeightedSum' needs at least one weight");
            foreach (KeyValuePair<string, double> kv in Weights)
            {
                if (Array.IndexOf(EpisodeRecord.Measures.ToArray(), kv.Key) < 0)
                    throw new ArgumentException(
                        "reward 'WeightedSum' weights '" + kv.Key + "', which is not a measure; the measures are " +
                        string.Join(", ", EpisodeRecord.Measures) + ".");
                if (double.IsNaN(kv.Value) || double.IsInfinity(kv.Value))
                    throw new ArgumentException("reward 'WeightedSum' gives '" + kv.Key + "' a weight that is not finite");
            }
        }

        public Score Score(IReadOnlyList<EpisodeRecord> episodes)
        {
            string[] names = Reads.ToArray();
            double[] weights = names.Select(m => Weights[m]).ToArray();

            double value = RewardMath.RunningMean(episodes, e =>
            {
                double s = 0;
                for (int k = 0; k < names.Length; k++) s += weights[k] * e.Measure(names[k]);
                return s;
            });

            var parts = new List<KeyValuePair<string, double>>();
            for (int k = 0; k < names.Length; k++)
            {
                string m = names[k];
                double w = weights[k];
                parts.Add(new KeyValuePair<string, double>(m, RewardMath.RunningMean(episodes, e => w * e.Measure(m))));
            }
            return new Score(value, parts);
        }
    }
}
