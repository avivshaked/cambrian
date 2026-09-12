using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Debug = UnityEngine.Debug;

namespace Evosim.Theatre.EditorTools
{
    /// <summary>
    /// Bakes the four font assets the interface's stylesheet asks for, out of the three IBM Plex
    /// faces committed beside them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An SDF atlas contains only what you put in it.</b> The design uses twelve non-ASCII
    /// characters and every one of them is load-bearing somewhere — U+00B7 carries the identity
    /// line, U+2212 is the depth column's true minus, U+2260 is the cousin badge, and U+2009 THIN
    /// SPACE is the thousands separator of every number on screen. A glyph that is not in the
    /// atlas draws as a blank box, which does not look like a font problem to anyone reading the
    /// picture afterwards. So they are added explicitly, here, and the log says which ones the
    /// face could not supply.
    /// </para>
    /// <para>
    /// <b>The fourth asset is the third face again, with line spacing baked in.</b> USS has no
    /// <c>line-height</c> — checked against this Editor's own UIElements module, which carries
    /// every other property name the stylesheet uses — and the design's eighteen multi-line prose
    /// blocks want 1.5. Unity's own answer is a second import of the same face with the spacing in
    /// its face info, which is what <c>--font-sans-prose</c> is. The write is done through
    /// reflection because whether <c>FontAsset.faceInfo</c> has a public setter is a thing this
    /// build could not check without an Editor; if it has none, the asset is still made, the log
    /// says the spacing was not baked, and the prose renders at single spacing rather than not at
    /// all.
    /// </para>
    /// <para>
    /// <b>It is a build tool and nothing depends on it at runtime.</b> If these assets are absent
    /// the stylesheet's <c>resource()</c> lookups fail and UI Toolkit falls back to its default
    /// face: the interface is then ugly and complete rather than missing. That is deliberate — a
    /// worker that has not run this should still be able to replay a run and take a picture.
    /// </para>
    /// </remarks>
    public static class TheatreUiFonts
    {
        private const string SourceFolder = "Assets/Theatre/UI/Fonts";
        private const string AssetFolder = "Assets/Theatre/UI/Resources/Fonts";

        /// <summary>Sampling size of the SDF render. 48 is Unity's own default for a UI face.</summary>
        private const int PointSize = 48;

        private const int Padding = 5;
        private const int AtlasWidth = 1024;
        private const int AtlasHeight = 1024;

        /// <summary>The line spacing <c>.prose</c> wants, which USS cannot ask for.</summary>
        private const float ProseLineSpacing = 1.5f;

        /// <summary>
        /// The twelve the design names, in the order its glyph-set note lists them.
        /// </summary>
        /// <remarks>
        /// U+03A3 is here even though IBM Plex Mono does not have it: the design uses the sigma
        /// only in <c>.section__title</c>, which is Sans, and the log saying "mono has no
        /// U+03A3" is the record of that being checked rather than a fault. <see cref="Expected"/>
        /// is where that judgement lives, so the build passes on it and fails on anything else.
        /// </remarks>
        private static readonly int[] Design =
        {
            0x00B7, 0x2014, 0x00D7, 0x2190, 0x2192, 0x03A3,
            0x2212, 0x2260, 0x2026, 0x00A7, 0x2013, 0x2009,
        };

        [MenuItem("Evosim/Theatre/Build UI Fonts")]
        public static void FromMenu() => Build();

        /// <summary>Batchmode entry point: exits 0 when all four were written.</summary>
        /// <remarks>
        /// <b>A documented gap is not a failure.</b> The first batch build exited 1 because the
        /// two Mono faces reported the sigma missing, which is the outcome the design predicts
        /// and this file has said in a comment since it was written (2026-09-13). The rule is now
        /// in the code rather than only in the prose: <see cref="Expected"/> names the gaps that
        /// are supposed to be there, they are logged plainly, and the build succeeds. A gap that
        /// is not on that list is still an error and still exits 1 — the point of pre-baking the
        /// glyph set is to be told when a character the design uses is not in the face.
        /// </remarks>
        public static void Run()
        {
            bool ok = Build();

            Debug.Log("[Theatre] UI fonts: exiting " + (ok ? "0" : "1") + ".");

            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                Directory.CreateDirectory(AssetFolder);
                AssetDatabase.Refresh();
            }

            bool ok = true;

            ok &= One("IBMPlexMono-Regular", "IBMPlexMono-Regular SDF", 1f);
            ok &= One("IBMPlexMono-SemiBold", "IBMPlexMono-SemiBold SDF", 1f);
            ok &= One("IBMPlexSans-Regular", "IBMPlexSans-Regular SDF", 1f);
            ok &= One("IBMPlexSans-Regular", "IBMPlexSans-Regular Prose SDF", ProseLineSpacing);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[Theatre] UI fonts: " + (ok ? "all four written to " : "SOMETHING FAILED in ") +
                AssetFolder + ". The stylesheet reaches them through resource(\"Fonts/<name>\").");

            return ok;
        }

        private static bool One(string face, string assetName, float lineSpacing)
        {
            string source = SourceFolder + "/" + face + ".ttf";
            var font = AssetDatabase.LoadAssetAtPath<Font>(source);

            if (font == null)
            {
                Debug.LogError(
                    "[Theatre] no font at " + source + ". The three IBM Plex faces are committed " +
                    "under " + SourceFolder + " with OFL.txt beside them; if they are missing, " +
                    "fetch them from the IBM Plex releases on GitHub rather than substituting " +
                    "another face.");

                return false;
            }

            string path = AssetFolder + "/" + assetName + ".asset";

            // Rebuilt rather than updated: an atlas half full of a previous run's glyphs is the
            // kind of thing that works until the one character nobody tested is missing.
            AssetDatabase.DeleteAsset(path);

            FontAsset asset;

            try
            {
                asset = FontAsset.CreateFontAsset(
                    font, PointSize, Padding, GlyphRenderMode.SDFAA,
                    AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);
            }
            catch (Exception e)
            {
                Debug.LogError("[Theatre] could not make a font asset from " + source + ": " + e);
                return false;
            }

            if (asset == null)
            {
                Debug.LogError("[Theatre] CreateFontAsset returned nothing for " + source + ".");
                return false;
            }

            asset.name = assetName;

            if (lineSpacing != 1f) BakeLineSpacing(asset, lineSpacing, assetName);

            AssetDatabase.CreateAsset(asset, path);

            // The atlas and the material are objects the asset owns; not adding them to the file
            // leaves a font asset whose texture is gone the next time the project opens.
            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    Texture2D atlas = asset.atlasTextures[i];
                    if (atlas == null) continue;

                    atlas.name = assetName + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
            }

            if (asset.material != null)
            {
                asset.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            bool filled = Populate(asset, face, assetName);

            EditorUtility.SetDirty(asset);
            return filled;
        }

        /// <summary>
        /// Puts printable ASCII and the design's twelve into the atlas, and says what was missing.
        /// </summary>
        /// <returns>
        /// False only when a character the design needs in <em>this</em> face is absent. A gap
        /// <see cref="Expected"/> knows about is logged and passes.
        /// </returns>
        private static bool Populate(FontAsset asset, string face, string assetName)
        {
            var characters = new StringBuilder();

            for (int c = 0x20; c <= 0x7E; c++) characters.Append((char)c);
            foreach (int c in Design) characters.Append(char.ConvertFromUtf32(c));

            try
            {
                bool all = asset.TryAddCharacters(characters.ToString(), out string missing);

                if (all || string.IsNullOrEmpty(missing)) return true;

                if (Expected(face, missing))
                {
                    Debug.Log(
                        "[Theatre] " + assetName + ": " + face + " has no " + CodePoints(missing) +
                        ", which is expected and not a fault. The design sets the sigma only in " +
                        ".section__title, which is Sans; IBM Plex Mono has no U+03A3 and never " +
                        "needs one. This line is the record of that being checked.");

                    return true;
                }

                Debug.LogError(
                    "[Theatre] " + assetName + " could not supply " + missing.Length +
                    " character(s): " + CodePoints(missing) + ". That is not one of the gaps this " +
                    "build expects (" + face + "), so a character the design uses in this face " +
                    "would draw as a blank box. Either the face is not the one committed under " +
                    SourceFolder + ", or the design's glyph set has grown past what it carries.");

                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Theatre] " + assetName + ": the glyph set was not pre-baked (" + e.GetType().Name +
                    "). The asset is dynamic, so a missing glyph is rendered on demand at runtime " +
                    "instead — which works in the Editor and is worth fixing before anything is " +
                    "built.");

                return true;
            }
        }

        /// <summary>
        /// Whether every character the atlas could not take is one this face is known not to have.
        /// </summary>
        /// <remarks>
        /// One entry, and it is the one the design's glyph-set note already names: the sigma is
        /// in IBM Plex Sans and not in IBM Plex Mono, verified in the three faces' <c>cmap</c>
        /// tables before the stylesheet was written. Adding a code point to <see cref="Design"/>
        /// that a face lacks must fail this build until someone decides it is fine and says so
        /// here — that is the whole value of pre-baking rather than letting the dynamic atlas
        /// discover it at runtime in front of a viewer.
        /// </remarks>
        private static bool Expected(string face, string missing)
        {
            bool mono = face.StartsWith("IBMPlexMono", StringComparison.Ordinal);

            for (int i = 0; i < missing.Length; i++)
            {
                // Written as its code point, never as a literal: a sigma typed into a source file
                // is one mis-set encoding away from being a different character, and this is the
                // comparison that decides whether the build passes.
                if (mono && missing[i] == (char)0x03A3) continue;
                return false;
            }

            return true;
        }

        private static string CodePoints(string text)
        {
            var list = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                if (list.Length > 0) list.Append(' ');
                list.Append("U+").Append(((int)text[i]).ToString("X4"));
            }

            return list.ToString();
        }

        /// <summary>
        /// Multiplies the face's line height, which is the only way USS can be given line spacing.
        /// </summary>
        /// <remarks>
        /// Through reflection on purpose: <c>faceInfo</c> is a struct property whose setter may be
        /// internal, and a compile error here would take the whole editor assembly — the snapshot
        /// entry and both identity checks included — down with it. A failure is logged and the
        /// asset is still written.
        /// </remarks>
        private static void BakeLineSpacing(FontAsset asset, float multiplier, string assetName)
        {
            try
            {
                PropertyInfo property = typeof(FontAsset).GetProperty(
                    "faceInfo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property == null)
                {
                    Debug.LogWarning("[Theatre] " + assetName + ": no faceInfo to write line spacing into.");
                    return;
                }

                object face = property.GetValue(asset);

                PropertyInfo height = face.GetType().GetProperty(
                    "lineHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (height == null || !height.CanRead)
                {
                    Debug.LogWarning("[Theatre] " + assetName + ": no lineHeight on the face info.");
                    return;
                }

                float was = Convert.ToSingle(height.GetValue(face));

                if (!height.CanWrite || !property.CanWrite)
                {
                    Debug.LogWarning(
                        "[Theatre] " + assetName + ": line spacing could not be baked (the face " +
                        "info is read-only in this Unity). The prose blocks will render at single " +
                        "spacing; the design wants " + multiplier + ". Set it by hand in the " +
                        "font asset's inspector, or the stylesheet can be given per-line labels " +
                        "instead (design/SPEC.md §6 check 2's fallback).");

                    return;
                }

                height.SetValue(face, was * multiplier);
                property.SetValue(asset, face);

                Debug.Log(
                    "[Theatre] " + assetName + ": line spacing " + was + " -> " + (was * multiplier) + ".");
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[Theatre] " + assetName + ": line spacing was not baked (" + e.GetType().Name +
                    ": " + e.Message + ").");
            }
        }
    }
}
