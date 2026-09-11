using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic challenge definitions for the conjunction search experiment.
///
/// Design: 14 rounds total per session.
/// The agent is gaze-contingent in every round, in either voice condition.
///
/// All challenges are fixed (seed 42) so every participant gets the exact same
/// targets, distractors, and shelf positions.
/// </summary>
public static class ChallengeSet
{
    // Two voice blocks; guidance remains gaze-contingent throughout.
    public const int RoundsPerBlock = 7;
    public const int BlockCount = 2;
    public const int TotalRounds = RoundsPerBlock * BlockCount; // 14
    public const int ObjectsPerRound = 56;
    public static int DebugRoundCountOverride { get; set; }
    public static int RoundCount =>
        DebugRoundCountOverride > 0
            ? Mathf.Clamp(DebugRoundCountOverride, 1, TotalRounds)
            : TotalRounds;

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
        public int blockIndex;       // 0-1: counterbalanced voice blocks
        public ObjectDef target;
        public ObjectDef[] objects;  // all 56
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
    /// All rounds use gaze-aware tips, independent of participant or voice.
    /// </summary>
    public static bool IsGazeAware(int roundIndex, int participantNumber)
    {
        _ = roundIndex;
        _ = participantNumber;
        return true;
    }

    /// <summary>
    /// Returns the condition label for a round.
    /// </summary>
    public static string GetConditionLabel(int roundIndex, int participantNumber)
    {
        return "gaze_aware";
    }

    public static RoundDef PracticeRound(int practiceIndex)
    {
        // Separate layouts, excluded from experimental round counts and seed sequence.
        var rng = new System.Random(900 + practiceIndex);
        var target = MakeObj(practiceIndex == 0 ? 0 : 1, practiceIndex == 0 ? 0 : 1);
        var objects = new ObjectDef[ObjectsPerRound];
        for (int i = 0; i < objects.Length; i++)
        {
            ObjectDef candidate;
            do { candidate = MakeObj(rng.Next(Shapes.Length), rng.Next(ColorNames.Length)); }
            while (candidate.shape == target.shape && candidate.color == target.color);
            objects[i] = candidate;
        }
        objects[rng.Next(objects.Length)] = target;
        return new RoundDef { roundIndex = practiceIndex == 0 ? 0 : RoundsPerBlock,
            blockIndex = practiceIndex, target = target, objects = objects };
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
                blockIndex = r / RoundsPerBlock,
                target = target,
                objects = objects.ToArray()
            };
        }

        Debug.Log($"[ChallengeSet] Generated {TotalRounds} deterministic rounds (always gaze-contingent)");
    }

    static ObjectDef MakeObj(int si, int ci) => new ObjectDef
    {
        shape = Shapes[si],
        color = ColorNames[ci],
        colorValue = ColorValues[ci]
    };
}
