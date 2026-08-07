using System;
using System.Collections.Generic;

namespace Evosim.Core
{
    /// <summary>
    /// Run settings to and from JSON — DESIGN.md §5A.10 and §7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written indented, because this is the file a person opens to set up an experiment. It is
    /// the one artefact in a run directory meant to be edited by hand.
    /// </para>
    /// <para>
    /// <b>The hash is written alongside the settings, and checked on load.</b> If they disagree,
    /// the file was hand-edited — which is expected and allowed, since editing it is the point —
    /// but the stored hash is then stale and every result filed under it is mislabelled. So the
    /// load reports the discrepancy rather than silently recomputing, and the caller decides.
    /// </para>
    /// </remarks>
    public static class RunConfigJson
    {
        public const int FormatVersion = 1;

        public static string Write(RunConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var w = new Json.Writer(indent: true);
            w.BeginObject();
            w.Field("format", FormatVersion);

            // Written for the reader's benefit and checked on load. Not authoritative — the
            // settings below are.
            w.Field("configHash", config.Hash());

            w.BeginObject("economy")
                .Field("perOffspringOverheadJoules", config.PerOffspringOverheadJoules)
                .Field("workCostMultiplier", config.WorkCostMultiplier)
                .Field("neuralCostPerNeuronWatts", config.NeuralCostPerNeuronWatts)
                .Field("neuralCostPerConnectionWatts", config.NeuralCostPerConnectionWatts)
                .Field("cellTypeMutationChance", config.CellTypeMutationChance)
                .EndObject();

            w.BeginObject("population")
                .Field("minimumPopulation", config.MinimumPopulation)
                .Field("maximumPopulation", config.MaximumPopulation)
                .Field("floorSpawnsPerStep", config.FloorSpawnsPerStep)
                .Field("founderEnergyJoules", config.FounderEnergyJoules)
                .Field("founderDepthSpread", config.FounderDepthSpread)
                .Field("worldAreaSquareMetres", config.WorldAreaSquareMetres)
                .Field("lightLayerMetres", config.LightLayerMetres)
                .Field("worldDepthMetres", config.WorldDepthMetres)
                .Field("nutrientSinkMetresPerSecond", config.NutrientSinkMetresPerSecond)
                .EndObject();

            w.BeginObject("light")
                .Field("surfaceIrradiance", config.Light.SurfaceIrradiance)
                .Field("attenuationDepth", config.Light.AttenuationDepth)
                .EndObject();

            w.BeginObject("fluid")
                .Field("density", config.Fluid.Density)
                .Field("dragCoefficient", config.Fluid.DragCoefficient)
                .Field("addedMassCoefficient", config.Fluid.AddedMassCoefficient)
                .Field("panelsPerAxis", config.Fluid.PanelsPerAxis)
                .EndObject();

            w.BeginObject("development")
                .Field("maxParts", config.Development.MaxParts)
                .Field("maxDepth", config.Development.MaxDepth)
                .Field("minPartVolume", config.Development.MinPartVolume)
                .Field("maxPartVolume", config.Development.MaxPartVolume)
                .Field("minPartHalfExtent", config.Development.MinPartHalfExtent)
                .EndObject();

            w.BeginObject("mutation")
                .Field("scalarChance", config.Mutation.ScalarChance)
                .Field("scalarStdDev", config.Mutation.ScalarStdDev)
                .Field("addNodeChance", config.Mutation.AddNodeChance)
                .Field("newNodeHalfExtent", config.Mutation.NewNodeHalfExtent)
                .Field("nodeExtinctionHalfExtent", config.Mutation.NodeExtinctionHalfExtent)
                .Field("addEdgeChance", config.Mutation.AddEdgeChance)
                .Field("removeEdgeChance", config.Mutation.RemoveEdgeChance)
                .Field("addNeuronChance", config.Mutation.AddNeuronChance)
                .Field("removeNeuronChance", config.Mutation.RemoveNeuronChance)
                .Field("rewireInputChance", config.Mutation.RewireInputChance)
                .Field("neuronOpChance", config.Mutation.NeuronOpChance)
                .Field("jointTypeChance", config.Mutation.JointTypeChance)
                .Field("flagChance", config.Mutation.FlagChance)
                .Field("recursiveLimitChance", config.Mutation.RecursiveLimitChance)
                .Field("cellTypeChance", config.Mutation.CellTypeChance)
                .Field("shapeChance", config.Mutation.ShapeChance)
                .Field("broodSizeChance", config.Mutation.BroodSizeChance)
                .Field("endowmentChance", config.Mutation.EndowmentChance)
                .Field("maxBroodSize", config.Mutation.MaxBroodSize)
                .Field("maxNodes", config.Mutation.MaxNodes)
                .EndObject();

            CellTypeJson.WriteRegistry(w, "cellTypes", config.CellTypes);
            WriteGenomeOptions(w, config.Genome);

            w.EndObject();
            return w.ToString();
        }

        /// <param name="text">Contents of a <c>config.json</c>.</param>
        /// <param name="hashMismatch">
        /// Set when the stored hash does not match the settings that were read — see the class
        /// remarks. Not an error, but never something to ignore silently.
        /// </param>
        public static RunConfig Read(string text, out string hashMismatch)
        {
            JsonNode root = Json.Parse(text);

            int format = root["format"].AsInt();
            if (format != FormatVersion)
            {
                throw new FormatException(
                    $"Run settings are format {format}, this build reads {FormatVersion}.");
            }

            JsonNode economy = root["economy"];
            JsonNode population = root["population"];
            JsonNode fluid = root["fluid"];
            JsonNode light = root["light"];
            JsonNode development = root["development"];

            var config = new RunConfig
            {
                PerOffspringOverheadJoules = economy["perOffspringOverheadJoules"].AsFloat(),
                WorkCostMultiplier = economy["workCostMultiplier"].AsFloat(),
                NeuralCostPerNeuronWatts = economy["neuralCostPerNeuronWatts"].AsFloat(),
                NeuralCostPerConnectionWatts = economy["neuralCostPerConnectionWatts"].AsFloat(),
                CellTypeMutationChance = economy["cellTypeMutationChance"].AsFloat(),

                MinimumPopulation = population["minimumPopulation"].AsInt(),
                MaximumPopulation = population["maximumPopulation"].AsInt(),
                FloorSpawnsPerStep = population["floorSpawnsPerStep"].AsInt(),
                FounderEnergyJoules = population["founderEnergyJoules"].AsFloat(),
                FounderDepthSpread = population["founderDepthSpread"].AsFloat(),
                WorldAreaSquareMetres = population["worldAreaSquareMetres"].AsFloat(),
                LightLayerMetres = population["lightLayerMetres"].AsFloat(),
                WorldDepthMetres = population["worldDepthMetres"].AsFloat(),
                NutrientSinkMetresPerSecond = population["nutrientSinkMetresPerSecond"].AsFloat(),

                Light = new LightModel(
                    light["surfaceIrradiance"].AsFloat(),
                    light["attenuationDepth"].AsFloat()),

                Fluid = new FluidConfig
                {
                    Density = fluid["density"].AsFloat(),
                    DragCoefficient = fluid["dragCoefficient"].AsFloat(),
                    AddedMassCoefficient = fluid["addedMassCoefficient"].AsFloat(),
                    PanelsPerAxis = fluid["panelsPerAxis"].AsInt(),
                },

                Development = new DevelopmentLimits
                {
                    MaxParts = development["maxParts"].AsInt(),
                    MaxDepth = development["maxDepth"].AsInt(),
                    MinPartVolume = development["minPartVolume"].AsFloat(),
                    MaxPartVolume = development["maxPartVolume"].AsFloat(),
                    MinPartHalfExtent = development["minPartHalfExtent"].AsFloat(),
                },

                CellTypes = CellTypeJson.ReadRegistry(root["cellTypes"]),
                Genome = ReadGenomeOptions(root["genome"]),
                Mutation = ReadMutationRates(root["mutation"]),
            };

            string stored = root["configHash"].AsString();
            string actual = config.Hash();

            hashMismatch = stored == actual
                ? null
                : $"Stored hash {stored} does not match the settings in this file ({actual}). " +
                  "The file was edited after it was written, which is fine — but results filed " +
                  "under the stored hash were produced by different settings.";

            return config;
        }

        public static RunConfig Read(string text) => Read(text, out _);

        private static MutationRates ReadMutationRates(JsonNode n) => new MutationRates
        {
            ScalarChance = n["scalarChance"].AsFloat(),
            ScalarStdDev = n["scalarStdDev"].AsFloat(),
            AddNodeChance = n["addNodeChance"].AsFloat(),
            NewNodeHalfExtent = n["newNodeHalfExtent"].AsFloat(),
            NodeExtinctionHalfExtent = n["nodeExtinctionHalfExtent"].AsFloat(),
            AddEdgeChance = n["addEdgeChance"].AsFloat(),
            RemoveEdgeChance = n["removeEdgeChance"].AsFloat(),
            AddNeuronChance = n["addNeuronChance"].AsFloat(),
            RemoveNeuronChance = n["removeNeuronChance"].AsFloat(),
            RewireInputChance = n["rewireInputChance"].AsFloat(),
            NeuronOpChance = n["neuronOpChance"].AsFloat(),
            JointTypeChance = n["jointTypeChance"].AsFloat(),
            FlagChance = n["flagChance"].AsFloat(),
            RecursiveLimitChance = n["recursiveLimitChance"].AsFloat(),
            CellTypeChance = n["cellTypeChance"].AsFloat(),
            ShapeChance = n["shapeChance"].AsFloat(),
            BroodSizeChance = n["broodSizeChance"].AsFloat(),
            EndowmentChance = n["endowmentChance"].AsFloat(),
            MaxBroodSize = n["maxBroodSize"].AsInt(),
            MaxNodes = n["maxNodes"].AsInt(),
        };

        // ---------------------------------------------------------------- genome options

        private static void WriteGenomeOptions(Json.Writer w, RandomGenomeOptions g)
        {
            w.BeginObject("genome")
                .Field("minNodes", g.MinNodes)
                .Field("maxNodes", g.MaxNodes)
                .Field("maxEdgesPerNode", g.MaxEdgesPerNode)
                .Field("minRecursiveLimit", g.MinRecursiveLimit)
                .Field("maxRecursiveLimit", g.MaxRecursiveLimit)
                .Field("minHalfExtent", g.MinHalfExtent)
                .Field("maxHalfExtent", g.MaxHalfExtent)
                .Field("minEdgeScale", g.MinEdgeScale)
                .Field("maxEdgeScale", g.MaxEdgeScale)
                .Field("reflectChance", g.ReflectChance)
                .Field("terminalChance", g.TerminalChance)
                .Field("rotateChance", g.RotateChance)
                .Field("maxEdgeTiltDegrees", g.MaxEdgeTiltDegrees)
                .Field("minNeuronsPerNode", g.MinNeuronsPerNode)
                .Field("maxNeuronsPerNode", g.MaxNeuronsPerNode)
                .Field("minOscillatorHz", g.MinOscillatorHz)
                .Field("maxOscillatorHz", g.MaxOscillatorHz)
                .Field("minJointLimit", g.MinJointLimit)
                .Field("maxJointLimit", g.MaxJointLimit)
                .Field("minLinkHalfExtent", g.MinLinkHalfExtent)
                .Field("maxLinkHalfExtent", g.MaxLinkHalfExtent)
                .Field("linkChance", g.LinkChance)
                .Field("minLinkPower", g.MinLinkPower)
                .Field("maxLinkPower", g.MaxLinkPower)
                .Field("minBroodSize", g.MinBroodSize)
                .Field("maxBroodSize", g.MaxBroodSize)
                .Field("minOffspringEndowment", g.MinOffspringEndowment)
                .Field("maxOffspringEndowment", g.MaxOffspringEndowment)
                .Field("founderTailChance", g.FounderTailChance);

            w.BeginArray("jointTypes");
            foreach (JointType t in g.JointTypes) w.Value(t.ToString());
            w.EndArray();

            w.BeginArray("bodyCellTypes");
            foreach (string id in g.BodyCellTypes) w.Value(id);
            w.EndArray();

            w.BeginArray("shapeChoices");
            foreach (string id in g.ShapeIdChoices) w.Value(id);
            w.EndArray();

            w.BeginArray("founderCellTypes");
            foreach (string id in g.FounderCellTypes) w.Value(id);
            w.EndArray();

            w.EndObject();
        }

        private static RandomGenomeOptions ReadGenomeOptions(JsonNode n)
        {
            var jointTypes = new List<JointType>();
            foreach (JsonNode t in n["jointTypes"].Items())
            {
                string name = t.AsString();
                if (!Enum.TryParse(name, out JointType parsed) ||
                    !Enum.IsDefined(typeof(JointType), parsed))
                {
                    throw new FormatException(
                        $"'{name}' is not a JointType. Known: " +
                        string.Join(", ", Enum.GetNames(typeof(JointType))) + ".");
                }
                jointTypes.Add(parsed);
            }

            var bodyCellTypes = new List<string>();
            foreach (JsonNode t in n["bodyCellTypes"].Items()) bodyCellTypes.Add(t.AsString());

            var shapeChoices = new List<string>();
            foreach (JsonNode t in n["shapeChoices"].Items()) shapeChoices.Add(t.AsString());

            var founderCellTypes = new List<string>();
            foreach (JsonNode t in n["founderCellTypes"].Items()) founderCellTypes.Add(t.AsString());

            return new RandomGenomeOptions
            {
                MinNodes = n["minNodes"].AsInt(),
                MaxNodes = n["maxNodes"].AsInt(),
                MaxEdgesPerNode = n["maxEdgesPerNode"].AsInt(),
                MinRecursiveLimit = n["minRecursiveLimit"].AsInt(),
                MaxRecursiveLimit = n["maxRecursiveLimit"].AsInt(),
                MinHalfExtent = n["minHalfExtent"].AsFloat(),
                MaxHalfExtent = n["maxHalfExtent"].AsFloat(),
                MinEdgeScale = n["minEdgeScale"].AsFloat(),
                MaxEdgeScale = n["maxEdgeScale"].AsFloat(),
                ReflectChance = n["reflectChance"].AsFloat(),
                TerminalChance = n["terminalChance"].AsFloat(),
                RotateChance = n["rotateChance"].AsFloat(),
                MaxEdgeTiltDegrees = n["maxEdgeTiltDegrees"].AsFloat(),
                MinNeuronsPerNode = n["minNeuronsPerNode"].AsInt(),
                MaxNeuronsPerNode = n["maxNeuronsPerNode"].AsInt(),
                MinOscillatorHz = n["minOscillatorHz"].AsFloat(),
                MaxOscillatorHz = n["maxOscillatorHz"].AsFloat(),
                MinJointLimit = n["minJointLimit"].AsFloat(),
                MaxJointLimit = n["maxJointLimit"].AsFloat(),
                MinLinkHalfExtent = n["minLinkHalfExtent"].AsFloat(),
                MaxLinkHalfExtent = n["maxLinkHalfExtent"].AsFloat(),
                LinkChance = n["linkChance"].AsFloat(),
                MinLinkPower = n["minLinkPower"].AsFloat(),
                MaxLinkPower = n["maxLinkPower"].AsFloat(),
                MinBroodSize = n["minBroodSize"].AsInt(),
                MaxBroodSize = n["maxBroodSize"].AsInt(),
                MinOffspringEndowment = n["minOffspringEndowment"].AsFloat(),
                MaxOffspringEndowment = n["maxOffspringEndowment"].AsFloat(),
                FounderTailChance = n["founderTailChance"].AsFloat(),
                JointTypes = jointTypes.ToArray(),
                BodyCellTypes = bodyCellTypes.ToArray(),
                ShapeIdChoices = shapeChoices.ToArray(),
                FounderCellTypes = founderCellTypes.ToArray(),
            };
        }
    }
}
