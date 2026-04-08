using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic challenge definitions for the conjunction search experiment.
///
/// Design: 14 rounds total per session.
/// Tip condition alternates every round:
/// - odd-numbered rounds (1,3,5,...) are gaze-unaware
/// - even-numbered rounds (2,4,6,...) are gaze-aware
///
/// All challenges are fixed (seed 42) so every participant gets the exact same
/// targets, distractors, and shelf positions. The ONLY variable is the
/// round-level gaze-aware/unaware alternation schedule.
/// </summary>
public static class ChallengeSet
{
    // Legacy constants kept for compatibility with logging/analysis code.
    public const int RoundsPerBlock = 7;
    public const int BlockCount = 2;
    public const int TotalRounds = RoundsPerBlock * BlockCount; // 14
    public const int ObjectsPerRound = 42;
    public static int RoundCount => TotalRounds;

    static readonly string[] Shapes = { "Sphere", "Cube", "Pyramid", "Cylinder", "Star", "Capsule" };
    static readonly string[] ColorNames = { "Red", "Blue", "Yellow", "Purple" };
    static readonly Color[] ColorValues =
    {
        new Color(0.9f, 0.15f, 0.15f, 1f),
        new Color(0.15f, 0.35f, 0.9f, 1f),
        new Color(0.95f, 0.85f, 0.1f, 1f),
        new Color(0.6f, 0.15f, 0.85f, 1f),
    };

    public struct ObjectDef
    {
        public string shape;
        public string color;
        public Color colorValue;
    }

    public struct RoundDef
    {
        public int roundIndex;       // 0-13
        public int blockIndex;       // 0 or 1 (alternating schedule index)
        public ObjectDef target;
        public ObjectDef[] objects;  // all 42
    }

    static RoundDef[] s_Rounds;

    /// <summary>All 14 rounds, pre-generated with fixed seed.</summary>
    public static RoundDef[] Rounds
    {
        get
        {
            if (s_Rounds == null) Generate();
            return s_Rounds;
        }
    }

    /// <summary>
    /// Returns whether a given round should use gaze-aware tips.
    /// Alternating schedule: round 0 (round 1 shown to participant) is unaware,
    /// round 1 is aware, and so on.
    /// </summary>
    public static bool IsGazeAware(int roundIndex, int participantNumber)
    {
        // participantNumber intentionally unused in fixed alternating mode
        _ = participantNumber;
        return (roundIndex % 2) == 1;
    }

    /// <summary>
    /// Returns the condition label for a round.
    /// </summary>
    public static string GetConditionLabel(int roundIndex, int participantNumber)
    {
        return IsGazeAware(roundIndex, participantNumber) ? "gaze_aware" : "gaze_unaware";
    }

    static void Generate()
    {
        var rng = new System.Random(42);

        // Build all possible targets (24 combos), pick 14 unique ones
        var allTargets = new List<(int si, int ci)>();
        for (int si = 0; si < Shapes.Length; si++)
            for (int ci = 0; ci < ColorNames.Length; ci++)
                allTargets.Add((si, ci));

        for (int i = allTargets.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (allTargets[i], allTargets[j]) = (allTargets[j], allTargets[i]);
        }

        s_Rounds = new RoundDef[TotalRounds];

        for (int r = 0; r < TotalRounds; r++)
        {
            var (tsi, tci) = allTargets[r];
            var target = MakeObj(tsi, tci);

            var sameColor = new List<ObjectDef>();
            for (int i = 0; i < Shapes.Length; i++)
                if (i != tsi) sameColor.Add(MakeObj(i, tci));

            var sameShape = new List<ObjectDef>();
            for (int i = 0; i < ColorNames.Length; i++)
                if (i != tci) sameShape.Add(MakeObj(tsi, i));

            var neutral = new List<ObjectDef>();
            for (int si = 0; si < Shapes.Length; si++)
            {
                if (si == tsi) continue;
                for (int ci = 0; ci < ColorNames.Length; ci++)
                {
                    if (ci == tci) continue;
                    neutral.Add(MakeObj(si, ci));
                }
            }

            var objects = new List<ObjectDef>(ObjectsPerRound);
            objects.Add(target);
            for (int i = 0; i < 13; i++) objects.Add(sameColor[rng.Next(sameColor.Count)]);
            for (int i = 0; i < 13; i++) objects.Add(sameShape[rng.Next(sameShape.Count)]);
            int neutralCount = ObjectsPerRound - objects.Count;
            for (int i = 0; i < neutralCount; i++) objects.Add(neutral[rng.Next(neutral.Count)]);

            for (int i = objects.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (objects[i], objects[j]) = (objects[j], objects[i]);
            }

            s_Rounds[r] = new RoundDef
            {
                roundIndex = r,
                blockIndex = r % 2,
                target = target,
                objects = objects.ToArray()
            };
        }

        Debug.Log($"[ChallengeSet] Generated {TotalRounds} deterministic rounds (alternating gaze-aware/unaware by round)");
    }

    static ObjectDef MakeObj(int si, int ci) => new ObjectDef
    {
        shape = Shapes[si],
        color = ColorNames[ci],
        colorValue = ColorValues[ci]
    };
}
