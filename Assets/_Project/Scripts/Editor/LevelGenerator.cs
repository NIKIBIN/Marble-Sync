using System;
using System.Collections.Generic;
using MarbleSync.Gameplay;
using MarbleSync.Simulation;
using UnityEditor;
using UnityEngine;

namespace MarbleSync.EditorTools
{
    /// <summary>
    /// Authors the prototype levels as ScriptableObject assets (a greybox slice of each world from the GDD)
    /// and checks every one of them with the solver.
    /// </summary>
    public static class LevelGenerator
    {
        public const string LevelFolder = "Assets/_Project/Data/Levels";
        public const string LibraryPath = "Assets/_Project/Data/LevelLibrary.asset";

        private static readonly Vector3 SeesawStart = Vector3.zero;
        private static readonly Vector3 SeesawDirection = new Vector3(1f, -0.15f, 0f);
        private const float ApproachX = -6f;
        private const float ApproachY = 0.9f;

        private static readonly (string name, Color color)[] Marbles =
        {
            ("Azul", new Color(0.2f, 0.45f, 1f)),
            ("Vermelha", new Color(1f, 0.25f, 0.2f)),
            ("Verde", new Color(0.25f, 0.8f, 0.3f)),
            ("Amarela", new Color(1f, 0.85f, 0.15f)),
            ("Roxa", new Color(0.65f, 0.3f, 0.9f))
        };

        // ------------------------------------------------------------ tiny lane DSL

        private struct Move
        {
            public Vector3 Delta;
        }

        private static Move X(float length, float drop) => new Move { Delta = new Vector3(length, -drop, 0f) };
        private static Move Zp(float length, float drop) => new Move { Delta = new Vector3(0f, -drop, length) };
        private static Move Zm(float length, float drop) => new Move { Delta = new Vector3(0f, -drop, -length) };

        private struct Place
        {
            public ElementKind Kind;
            public int Segment;
            public float Fraction;
            public LogicColor Color;
            public int Id;
            public bool StartsActive;
        }

        private static Place At(ElementKind kind, int segment, float fraction, LogicColor color = LogicColor.Blue, int id = 0, bool startsActive = true)
        {
            return new Place { Kind = kind, Segment = segment, Fraction = fraction, Color = color, Id = id, StartsActive = startsActive };
        }

        private sealed class LaneSpec
        {
            public int Marble;
            public Vector3 Start;
            public Move[] Moves = Array.Empty<Move>();
            public Place[] Places = Array.Empty<Place>();
        }

        private static LaneSpec Lane(int marble, float x, float y, float z, Move[] moves, params Place[] places)
        {
            return new LaneSpec { Marble = marble, Start = new Vector3(x, y, z), Moves = moves, Places = places };
        }

        private static MarbleLane BuildLane(LaneSpec spec)
        {
            var points = new List<Vector3> { spec.Start };
            Vector3 p = spec.Start;
            foreach (Move move in spec.Moves)
            {
                p += move.Delta;
                points.Add(p);
            }

            if (Mathf.Abs(p.z - spec.Start.z) > 0.001f)
            {
                throw new InvalidOperationException($"Lane {spec.Marble} must return to its z lane before the approach.");
            }
            if (p.x >= ApproachX - 0.5f || p.y <= ApproachY)
            {
                throw new InvalidOperationException($"Lane {spec.Marble} ends at {p}, it must stay left of x={ApproachX} and above y={ApproachY}.");
            }

            points.Add(new Vector3(ApproachX, ApproachY, spec.Start.z));
            points.Add(SeesawStart);

            var path = new TrackPath(points.ToArray());
            var elements = new ElementDef[spec.Places.Length];
            for (int i = 0; i < spec.Places.Length; i++)
            {
                Place place = spec.Places[i];
                float from = path.DistanceAtPoint(place.Segment);
                float to = path.DistanceAtPoint(place.Segment + 1);
                float distance = Mathf.Lerp(from, to, place.Fraction);
                elements[i] = new ElementDef(place.Kind, distance, place.Color, place.Id, place.StartsActive);
            }
            Array.Sort(elements, (a, b) => a.Distance.CompareTo(b.Distance));

            return new MarbleLane
            {
                MarbleName = Marbles[spec.Marble].name,
                MarbleColor = Marbles[spec.Marble].color,
                Waypoints = points.ToArray(),
                Elements = elements
            };
        }

        private static SimTuning Tuning()
        {
            return new SimTuning { SeesawLength = 6f };
        }

        // ------------------------------------------------------------ level list

        private sealed class LevelSpec
        {
            public string Id;
            public string Title;
            public string Intro;
            public int Sponges;
            public bool Timers;
            public LaneSpec[] Lanes;
        }

        private static IEnumerable<LevelSpec> Levels()
        {
            yield return new LevelSpec
            {
                Id = "1-1", Title = "Tutorial",
                Intro = "Clique nas bolinhas para soltá-las. Todas precisam entrar na gangorra com no máximo 0,5 s de diferença: quem chega cedo demais passa da metade e fecha a gangorra.",
                Lanes = new[]
                {
                    Lane(0, -20f, 4f, -1f, new[] { X(10f, 2f) }),
                    Lane(1, -14f, 3f, 1f, new[] { X(4f, 1f) })
                }
            };

            yield return new LevelSpec
            {
                Id = "1-2", Title = "Curvas",
                Intro = "Curvas quadradas cortam a velocidade. Uma curva custa mais quando a bolinha chega rápida e o trecho seguinte é curto.",
                Lanes = new[]
                {
                    Lane(0, -22f, 6f, -2f, new[] { X(5f, 1.2f), Zm(3f, 0.6f), X(5f, 1.2f), Zp(3f, 0.6f), X(2f, 0.5f) }),
                    Lane(1, -20f, 5f, 0f, new[] { X(10f, 3f) }),
                    Lane(2, -18f, 5f, 2f, new[] { X(3f, 1f), Zp(2f, 0.5f), X(3f, 1f), Zm(2f, 0.5f), X(2f, 0.5f) })
                }
            };

            yield return new LevelSpec
            {
                Id = "1-4", Title = "Cinco de uma vez",
                Intro = "Cinco bolinhas. Observe quem é mais lenta e monte a ordem de soltura a partir dela.",
                Lanes = new[]
                {
                    Lane(0, -24f, 7f, -4f, new[] { X(6f, 1.5f), Zm(1.5f, 0.4f), X(4f, 1f), Zp(1.5f, 0.4f), X(4f, 1f) }),
                    Lane(1, -18f, 5f, -2f, new[] { X(8f, 2.5f) }),
                    Lane(2, -22f, 6f, 0f, new[] { X(3f, 1f), Zp(1f, 0.3f), X(3f, 1f), Zm(1f, 0.3f), X(3f, 1f), Zp(1f, 0.3f), X(3f, 0.6f), Zm(1f, 0.3f) }),
                    Lane(3, -16f, 4f, 2f, new[] { X(2f, 0.6f), Zp(1f, 0.3f), X(2f, 0.6f), Zm(1f, 0.3f), X(2f, 0.6f) }),
                    Lane(4, -26f, 8f, 4f, new[] { X(12f, 3f), X(4f, 1.5f) })
                }
            };

            yield return new LevelSpec
            {
                Id = "2-1", Title = "Mel",
                Intro = "Mel derruba a velocidade máxima pela metade. Os dois trajetos são iguais, só muda onde está o mel.",
                Lanes = new[]
                {
                    Lane(0, -22f, 5f, -1f, new[] { X(12f, 3f) }, At(ElementKind.Honey, 0, 0.12f)),
                    Lane(1, -22f, 5f, 1f, new[] { X(12f, 3f) }, At(ElementKind.Honey, 0, 0.9f))
                }
            };

            yield return new LevelSpec
            {
                Id = "2-2", Title = "Óleo",
                Intro = "Óleo é o oposto do mel: mais velocidade e mais aceleração pelo resto do caminho.",
                Lanes = new[]
                {
                    Lane(0, -20f, 5f, -1f, new[] { X(4f, 1f), Zm(2f, 0.5f), X(6f, 1.5f), Zp(2f, 0.5f) }, At(ElementKind.Oil, 0, 0.3f)),
                    Lane(1, -18f, 4f, 1f, new[] { X(8f, 2f) }, At(ElementKind.Oil, 1, 0.5f))
                }
            };

            yield return new LevelSpec
            {
                Id = "2-3", Title = "Água",
                Intro = "Água lava o mel e o óleo e deixa a bolinha molhada. A água não acumula.",
                Lanes = new[]
                {
                    Lane(0, -22f, 6f, -2f, new[] { X(12f, 3f) }, At(ElementKind.Honey, 0, 0.1f), At(ElementKind.Water, 0, 0.5f)),
                    Lane(1, -20f, 5f, 0f, new[] { X(4f, 1f), Zp(1f, 0.4f), X(6f, 1.5f), Zm(1f, 0.4f) }, At(ElementKind.Oil, 0, 0.2f), At(ElementKind.Water, 2, 0.6f)),
                    Lane(2, -18f, 4f, 2f, new[] { X(8f, 2f) }, At(ElementKind.Honey, 0, 0.6f))
                }
            };

            yield return new LevelSpec
            {
                Id = "2-5", Title = "Esponja",
                Intro = "Antes de soltar a primeira bolinha, você pode usar a esponja: clique numa poça para removê-la. Reiniciar devolve a poça e a esponja.",
                Sponges = 1,
                Lanes = new[]
                {
                    Lane(0, -24f, 7f, -4f, new[] { X(6f, 1.5f), Zm(1.5f, 0.4f), X(6f, 1.5f), Zp(1.5f, 0.4f), X(2f, 0.5f) }, At(ElementKind.Honey, 0, 0.2f), At(ElementKind.Water, 2, 0.5f)),
                    Lane(1, -20f, 5f, -2f, new[] { X(10f, 2.5f) }, At(ElementKind.Oil, 0, 0.3f), At(ElementKind.Honey, 0, 0.7f)),
                    Lane(2, -22f, 6f, 0f, new[] { X(4f, 1f), Zm(1f, 0.3f), X(4f, 1f), Zp(1f, 0.3f), X(4f, 1f) }, At(ElementKind.Honey, 0, 0.3f)),
                    Lane(3, -18f, 4.5f, 2f, new[] { X(8f, 2f) }, At(ElementKind.Oil, 0, 0.2f), At(ElementKind.Water, 0, 0.8f)),
                    Lane(4, -26f, 8f, 4f, new[] { X(8f, 2f), Zm(1f, 0.3f), X(8f, 2f), Zp(1f, 0.3f), X(2f, 0.5f) }, At(ElementKind.Oil, 0, 0.1f), At(ElementKind.Honey, 2, 0.4f), At(ElementKind.Water, 4, 0.5f))
                }
            };

            yield return new LevelSpec
            {
                Id = "3-1", Title = "Portas lógicas",
                Intro = "Quando uma bolinha passa por um botão, todas as portas da mesma cor se invertem: abertas fecham, fechadas abrem.",
                Lanes = new[]
                {
                    Lane(0, -20f, 5f, -1f, new[] { X(10f, 2.5f) }, At(ElementKind.Button, 0, 0.2f, LogicColor.Blue, 1)),
                    Lane(1, -16f, 4f, 1f, new[] { X(6f, 1.5f) }, At(ElementKind.Door, 0, 0.5f, LogicColor.Blue, 0, true))
                }
            };

            yield return new LevelSpec
            {
                Id = "3-3", Title = "Timers",
                Intro = "Timers: clique numa bolinha antes de começar e ajuste o atraso com ▲▼ (passos de 0,25 s). T troca o gatilho; clique num sensor para contar a partir da passagem de uma bolinha por ali.",
                Timers = true,
                Lanes = new[]
                {
                    Lane(0, -26f, 8f, -3f, new[] { X(8f, 2f), Zm(2f, 0.5f), X(8f, 2f), Zp(2f, 0.5f), X(2f, 0.5f) }, At(ElementKind.Sensor, 0, 0.5f, LogicColor.Blue, 1), At(ElementKind.Sensor, 2, 0.5f, LogicColor.Blue, 2)),
                    Lane(1, -18f, 4.5f, -1f, new[] { X(8f, 2f) }, At(ElementKind.Honey, 0, 0.3f)),
                    Lane(2, -22f, 6f, 1f, new[] { X(4f, 1f), Zm(1f, 0.3f), X(4f, 1f), Zp(1f, 0.3f), X(4f, 1f) }, At(ElementKind.Sensor, 2, 0.5f, LogicColor.Blue, 3), At(ElementKind.Oil, 4, 0.2f)),
                    Lane(3, -16f, 4f, 3f, new[] { X(6f, 1.5f) })
                }
            };

            yield return new LevelSpec
            {
                Id = "4-1", Title = "Fogo",
                Intro = "O lança-chamas derrete bolinhas secas. O botão vermelho liga e desliga os lança-chamas vermelhos.",
                Lanes = new[]
                {
                    Lane(0, -20f, 5f, -1f, new[] { X(10f, 2.5f) }, At(ElementKind.Button, 0, 0.15f, LogicColor.Red, 1)),
                    Lane(1, -18f, 4.5f, 1f, new[] { X(8f, 2f) }, At(ElementKind.Flame, 0, 0.6f, LogicColor.Red, 0, true))
                }
            };

            yield return new LevelSpec
            {
                Id = "4-2", Title = "Óleo, fogo e água",
                Intro = "Óleo + fogo = bolinha em chamas: muito mais rápida, mas derrete em 1 s sem água. Molhada, ela atravessa o fogo uma vez.",
                Lanes = new[]
                {
                    Lane(0, -24f, 6f, -1f, new[] { X(14f, 3.5f) }, At(ElementKind.Oil, 0, 0.1f), At(ElementKind.Flame, 0, 0.3f, LogicColor.Red), At(ElementKind.Water, 0, 0.6f)),
                    Lane(1, -18f, 4.5f, 1f, new[] { X(8f, 2f) }, At(ElementKind.Water, 0, 0.2f), At(ElementKind.Flame, 0, 0.6f, LogicColor.Red))
                }
            };

            yield return new LevelSpec
            {
                Id = "4-3", Title = "Caramelo",
                Intro = "Mel + fogo carameliza: a bolinha para até você clicar nela de novo, e volta a rolar sem nenhum efeito. Use isso como uma segunda soltura.",
                Lanes = new[]
                {
                    Lane(0, -20f, 5f, -2f, new[] { X(10f, 2.5f) }),
                    Lane(1, -24f, 6f, 0f, new[] { X(14f, 3.5f) }, At(ElementKind.Honey, 0, 0.1f), At(ElementKind.Flame, 0, 0.4f, LogicColor.Red)),
                    Lane(2, -18f, 4.5f, 2f, new[] { X(4f, 1f), Zp(1.5f, 0.4f), X(4f, 1f), Zm(1.5f, 0.4f) })
                }
            };
        }

        // ------------------------------------------------------------ generation

        [MenuItem("Marble Sync/1. Generate Levels")]
        public static LevelLibrary Generate()
        {
            EnsureFolder(LevelFolder);
            var assets = new List<LevelDefinition>();
            var report = new System.Text.StringBuilder("Marble Sync levels:\n");

            foreach (LevelSpec spec in Levels())
            {
                var lanes = new MarbleLane[spec.Lanes.Length];
                for (int i = 0; i < lanes.Length; i++)
                {
                    lanes[i] = BuildLane(spec.Lanes[i]);
                }

                string path = $"{LevelFolder}/Level_{spec.Id}.asset";
                LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                bool created = level == null;
                if (created)
                {
                    level = ScriptableObject.CreateInstance<LevelDefinition>();
                }

                level.Author(spec.Id, spec.Title, 1, spec.Intro, string.Empty, spec.Sponges, spec.Timers, lanes, SeesawStart, SeesawDirection, Tuning());
                LevelSolver.Solution solution = LevelSolver.Solve(level.SimDef);
                string hint = spec.Timers ? BuildHint(level, solution) : string.Empty;
                level.Author(spec.Id, spec.Title, 1, spec.Intro, hint, spec.Sponges, spec.Timers, lanes, SeesawStart, SeesawDirection, Tuning());

                if (created)
                {
                    AssetDatabase.CreateAsset(level, path);
                }
                EditorUtility.SetDirty(level);
                assets.Add(level);

                report.Append($"  {spec.Id} {spec.Title}: {(solution.Solved ? "solvable" : "NOT SOLVED")} spread {solution.Spread:0.000}s releases [{string.Join(", ", solution.ReleaseSteps)}]\n");
                if (!solution.Solved)
                {
                    Debug.LogError($"Level {spec.Id} could not be solved by the search. Check its layout.");
                }
            }

            LevelLibrary library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<LevelLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.Author(assets.ToArray());
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log(report.ToString());
            return library;
        }

        /// <summary>A ready-made timer rule from a known solution: anchor the slowest-to-time marble on the first one released.</summary>
        private static string BuildHint(LevelDefinition level, LevelSolver.Solution solution)
        {
            int first = 0;
            int last = 0;
            for (int i = 0; i < solution.ReleaseSteps.Length; i++)
            {
                if (solution.ReleaseSteps[i] < solution.ReleaseSteps[first]) first = i;
                if (solution.ReleaseSteps[i] > solution.ReleaseSteps[last]) last = i;
            }
            float delay = (solution.ReleaseSteps[last] - solution.ReleaseSteps[first]) / level.Tuning.StepsPerSecond;
            float snapped = Mathf.Round(delay / level.Tuning.TimerStepSeconds) * level.Tuning.TimerStepSeconds;
            var rule = new TimerRule(last, TimerTrigger.MarbleReleased, snapped, first);
            return GameText.DescribeTimer(rule, level);
        }

        public static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
