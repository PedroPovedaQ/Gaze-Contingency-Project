using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic challenge definitions for the conjunction search experiment.
///
/// Design: 20 measured rounds total per session, plus separate practice.
/// The agent is gaze-contingent in every round, in either voice condition.
///
/// Seeded per participant, with equal absolute target-wall angle counts in both
/// blocks. The second block mirrors directions and independently shuffles order.
/// </summary>
public static class ChallengeSet
{
    // Two voice blocks; guidance remains gaze-contingent throughout.
    public const int RoundsPerBlock = 10;
    public const int BlockCount = 2;
    public const int TotalRounds = RoundsPerBlock * BlockCount; // 20
    public const int ObjectsPerRound = 168;
    public const string ScheduleVersion = "previous-wall-angle-v3-10-trials";
    const int k_PlaneCount = 8;
    const int k_ObjectsPerPlane = ObjectsPerRound / k_PlaneCount;
    // Optional short test run: 0 means use all 20 trials. A shorter run will not
    // necessarily finish both blocks or preserve the completed-trial angle balance.
    public static int DebugRoundCountOverride { get; set; }

    // How many measured trials the game will actually run. Clamp keeps a debug
    // request inside 1–20; it does not change the full plan generated below.
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

    // One object's identity: for example, "Cube" + "Red" + its display color.
    // Its position is assigned separately, so identity and location can be shuffled.
    public struct ObjectDef
    {
        public string shape;
        public string color;
        public Color colorValue;
    }

    // Everything needed to recreate one trial: its target, all object identities,
    // target wall/slot, geometry seed, and the pair linking it to the other block.
    public struct RoundDef
    {
        public int roundIndex;       // 0-19
        public int blockIndex;       // 0-1: counterbalanced voice blocks
        public ObjectDef target;
        public ObjectDef[] objects;  // all 168
        public int startPlane, targetPlane, targetSlot, layoutSeed, anglePair;
        // Theta is the shortest turn from the readiness cross wall to the target wall.
        // Example: start wall 7 (315°), target wall 0 (0°) gives +45°, not -315°.
        public int SignedTheta
        {
            get
            {
                int steps = (targetPlane - startPlane + 8) % 8;
                return (steps <= 4 ? steps : steps - 8) * 45;
            }
        }

        // Ignore left/right: both -135° and +135° count in the 135° category.
        public int AbsoluteTheta => System.Math.Abs(SignedTheta);
    }

    static RoundDef[] s_Rounds;
    static string s_Participant;
    /// <summary>
    /// Turn the coded participant ID into the starting number for our random draws.
    /// The same ID gives the same seed, so restarting reproduces the same schedule
    /// with this version of the generator. This is reproducible pseudorandomness.
    /// </summary>
    public static int ScheduleSeed
    {
        get
        {
            SessionConfig.EnsureParticipantId();
            // Establish the ID before planning, so a later auto-assignment cannot
            // accidentally change the target schedule when the run begins.
            // This fixed hash mixes each character into a number. ^ mixes bits;
            // multiplication spreads them; unchecked allows normal integer wraparound.
            // Stable across processes/platforms; string.GetHashCode is not.
            uint hash = 2166136261;
            foreach (char c in SessionConfig.ParticipantId ?? "") hash = unchecked((hash ^ c) * 16777619);
            // Clear the sign bit to return a nonnegative integer seed.
            return (int)(hash & 0x7fffffff);
        }
    }

    /// <summary>
    /// Get the complete measured-trial plan. Build it once, then reuse it each time
    /// the game or logger asks. A different participant ID causes a fresh plan.
    /// </summary>
    public static RoundDef[] Rounds
    {
        get
        {
            SessionConfig.EnsureParticipantId();
            if (s_Rounds == null || s_Participant != SessionConfig.ParticipantId) Generate();
            return s_Rounds;
        }
    }

    /// <summary>
    /// Write one CSV row per planned measured trial, including trials not yet played.
    /// Save angles, target identity, voice and seeds so analysis can reconstruct the
    /// assignment. Practice is separate; enabled_in_run flags debug-truncated trials.
    /// </summary>
    public static void WriteSchedule(System.IO.TextWriter writer)
    {
        writer.WriteLine("participant_id,schedule_version,schedule_seed,trial_index,block_index,trial_in_block,voice_condition,angle_pair,target_plane,wall_azimuth_deg,signed_theta_deg,absolute_theta_deg,target_slot,layout_seed,target_shape,target_color,enabled_in_run,start_plane,start_wall_azimuth_deg,theta_reference");
        foreach (var round in Rounds)
        {
            // Use the saved voice-block assignment, not whichever voice is speaking
            // right now. Trial/block indices start at 0. % 10 gives position within
            // the block: overall trial index 13 becomes position 3 in block 1.
            string voice = SessionConfig.VoiceBlocksEnabled ? SessionConfig.VoiceLabelForRound(round.roundIndex) : SessionConfig.VoiceTag;
            writer.WriteLine(System.FormattableString.Invariant($"{SessionConfig.ParticipantId},{ScheduleVersion},{ScheduleSeed},{round.roundIndex},{round.blockIndex},{round.roundIndex % RoundsPerBlock},{voice},{round.anglePair},{round.targetPlane},{round.targetPlane * 45},{round.SignedTheta},{round.AbsoluteTheta},{round.targetSlot},{round.layoutSeed},{round.target.shape},{round.target.color},{(round.roundIndex < RoundCount ? 1 : 0)},{round.startPlane},{round.startPlane * 45},previous_target_wall_center"));
        }
    }

    /// <summary>
    /// Answer whether a trial uses gaze-aware guidance: currently always yes.
    /// Keep the arguments for existing callers, but do not use them to alternate
    /// guidance. Both voice conditions receive gaze-contingent hints.
    /// </summary>
    public static bool IsGazeAware(int roundIndex, int participantNumber)
    {
        _ = roundIndex;
        _ = participantNumber;
        return true;
    }

    /// <summary>
    /// Give the logger the guidance label "gaze_aware" for every trial.
    /// Voice condition is recorded separately; this label does not identify the voice.
    /// </summary>
    public static string GetConditionLabel(int roundIndex, int participantNumber)
    {
        return "gaze_aware";
    }

    /// <summary>
    /// Build one of the two practice trials (practiceIndex 0 or 1).
    /// Use repeatable practice-only seeds, a red sphere first and a blue cube second.
    /// Practice does not consume any of the twenty measured trials or angle quotas.
    /// </summary>
    public static RoundDef PracticeRound(int practiceIndex)
    {
        // Separate layouts, excluded from experimental round counts and seed sequence.
        var rng = new System.Random(900 + practiceIndex);
        var target = MakeObj(practiceIndex == 0 ? 0 : 1, practiceIndex == 0 ? 0 : 1);
        var objects = new ObjectDef[ObjectsPerRound];
        for (int i = 0; i < objects.Length; i++)
        {
            // Draw a random shape/color. If it exactly matches the target, try again;
            // every object initially needs to be a distractor.
            ObjectDef candidate;
            do { candidate = MakeObj(rng.Next(Shapes.Length), rng.Next(ColorNames.Length)); }
            while (candidate.shape == target.shape && candidate.color == target.color);
            objects[i] = candidate;
        }
        // Replace one randomly chosen distractor, giving practice exactly one target.
        int targetIndex = rng.Next(objects.Length);
        objects[targetIndex] = target;
        // Indices 0 and 10 select the two voice blocks for practice. They are routing
        // indices, not claims that these practice attempts are measured trials.
        return new RoundDef { roundIndex = practiceIndex == 0 ? 0 : RoundsPerBlock,
            blockIndex = practiceIndex, target = target, objects = objects,
            startPlane = practiceIndex == 0 ? 0 : PracticeRound(0).targetPlane,
            targetPlane = targetIndex / k_ObjectsPerPlane, targetSlot = targetIndex % k_ObjectsPerPlane };
    }

    /// <summary>
    /// Build all twenty measured trials: balance angle categories, choose directions,
    /// shuffle each block, choose unique targets, and place each target on its wall.
    /// This chooses wall/slot and a layout seed; RotationalSearchLayout.Build uses
    /// that seed to scatter actual positions and heights within the wall boundaries.
    /// </summary>
    static void Generate()
    {
        s_Participant = SessionConfig.ParticipantId;
        var rng = new System.Random(ScheduleSeed);

        // 1. Make ten angle assignments. These numbers are units of 45°, not degrees:
        //    0, 1, 2, 3, 4 mean 0°, 45°, 90°, 135°, 180°. Repeat twice for equal counts.
        //    Each assignment gets a pair ID so we can find its mirror after shuffling.
        int[] magnitudes = { 0, 1, 2, 3, 4, 0, 1, 2, 3, 4 };
        var turnSteps = new int[RoundsPerBlock];
        // Flip a coin (Next(2) returns 0 or 1): keep the rightward turn or mirror it.
        // Example: magnitude 3 is a 135° right turn; its mirror is step 5 (225°,
        // or 135° left), because 8 - 3 = 5. % 8 wraps wall numbers into 0–7.
        // Zero means remain on the starting wall; 180° mirrors onto itself at step 4.
        for (int i = 0; i < turnSteps.Length; i++)
            turnSteps[i] = magnitudes[i] == 0 || rng.Next(2) == 0 ? magnitudes[i] : (8 - magnitudes[i]) % 8;

        // 2. Shuffle pair IDs separately within each block (Fisher–Yates shuffle).
        // Work backward, swap each entry with a randomly chosen entry at/before it.
        // Shuffling changes order only: it cannot add or remove an angle assignment.
        var pairOrder = new int[TotalRounds];
        for (int block = 0; block < BlockCount; block++)
        {
            int start = block * RoundsPerBlock;
            for (int i = 0; i < RoundsPerBlock; i++) pairOrder[start + i] = i;
            for (int i = RoundsPerBlock - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (pairOrder[start + i], pairOrder[start + j]) = (pairOrder[start + j], pairOrder[start + i]);
            }
        }

        // 3. List all 6 shapes × 4 colors = 24 possible targets. Shuffle the list,
        // then use its first 20 entries so no exact target identity repeats this run.
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
        // Both practices happen before measured trials. Continue from the last practice
        // target, then from each measured target, including across the block break.
        int startPlane = PracticeRound(1).targetPlane;

        for (int r = 0; r < TotalRounds; r++)
        {
            // 4. Build this trial's object identities. tsi/tci are the target's
            // shape/color indices into the lookup arrays at the top of this file.
            var (tsi, tci) = allTargets[r];
            var target = MakeObj(tsi, tci);

            // Distractor pool A: target color, different shape.
            var sameColor = new List<ObjectDef>();
            for (int i = 0; i < Shapes.Length; i++)
                if (i != tsi) sameColor.Add(MakeObj(i, tci));

            // Distractor pool B: target shape, different color.
            var sameShape = new List<ObjectDef>();
            for (int i = 0; i < ColorNames.Length; i++)
                if (i != tci) sameShape.Add(MakeObj(tsi, i));

            // Distractor pool C: neither the target color nor the target shape.
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

            // 1 target + 39 same-color + 39 same-shape + 89 neutral = 168 objects.
            // Draw with replacement from distractor pools: distractors may repeat,
            // but the exact target combination appears only once.
            var objects = new List<ObjectDef>(ObjectsPerRound);
            objects.Add(target);
            for (int i = 0; i < 39; i++) objects.Add(sameColor[rng.Next(sameColor.Count)]);
            for (int i = 0; i < 39; i++) objects.Add(sameShape[rng.Next(sameShape.Count)]);
            int neutralCount = ObjectsPerRound - objects.Count;
            for (int i = 0; i < neutralCount; i++) objects.Add(neutral[rng.Next(neutral.Count)]);

            // Mix the identities so distractor categories are not grouped together.
            for (int i = objects.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (objects[i], objects[j]) = (objects[j], objects[i]);
            }

            // 5. Apply this trial's shuffled angle assignment. Block two uses the
            // opposite relative turn for the same pair, keeping |theta| unchanged.
            int pair = pairOrder[r];
            int step = r < RoundsPerBlock ? turnSteps[pair] : (8 - turnSteps[pair]) % 8;
            int plane = (startPlane + step) % 8;
            // Pick one of the wall's 21 positions (indices 0–20). A slot is an index
            // into the randomized layout, not a fixed height or shelf location.
            int slot = rng.Next(k_ObjectsPerPlane);
            int targetIndex = objects.FindIndex(o => o.shape == target.shape && o.color == target.color);
            // Slots are stored wall by wall. Wall 5, slot 2 becomes 5 × 21 + 2 = 107.
            // Swap the target into that position instead of copying it: this keeps
            // exactly one target and preserves all distractor counts.
            int destination = plane * k_ObjectsPerPlane + slot;
            (objects[targetIndex], objects[destination]) = (objects[destination], objects[targetIndex]);

            // 6. Save the trial. Integer division r / 10 maps indices 0–9 to block 0
            // and 10–19 to block 1. The layout seed controls the separate horizontal
            // position/height draws, so a balanced wall angle need not equal the
            // object's exact bearing. Both the seed and actual positions are logged.
            s_Rounds[r] = new RoundDef
            {
                roundIndex = r,
                blockIndex = r / RoundsPerBlock,
                target = target,
                objects = objects.ToArray(), startPlane = startPlane, targetPlane = plane, targetSlot = slot,
                layoutSeed = rng.Next(1, int.MaxValue), anglePair = pair
            };
            startPlane = plane;
        }

        Debug.Log($"[ChallengeSet] Generated {TotalRounds} balanced rounds: {ScheduleVersion}, seed={ScheduleSeed}");
    }

    /// <summary>
    /// Turn shape/color array indices into an object identity; no randomness here.
    /// Example: si=1, ci=0 selects "Cube", "Red", and the red display color.
    /// </summary>
    static ObjectDef MakeObj(int si, int ci) => new ObjectDef
    {
        shape = Shapes[si],
        color = ColorNames[ci],
        colorValue = ColorValues[ci]
    };
}
