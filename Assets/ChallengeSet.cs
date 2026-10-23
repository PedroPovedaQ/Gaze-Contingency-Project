using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic challenge definitions for the conjunction search experiment.
///
/// Design: 80 measured rounds total per session, plus separate practice.
/// The agent is gaze-contingent in every round, in either voice condition.
///
/// Seeded per participant, with equal absolute target-wall angle counts in both
/// blocks. The second block mirrors directions and independently shuffles order.
/// </summary>
public static class ChallengeSet
{
    // Two voice blocks; guidance remains gaze-contingent throughout.
    public const int RoundsPerBlock = 40;
    public const int BlockCount = 2;
    public const int PracticeTrialCount = 1;
    public const int TotalRounds = RoundsPerBlock * BlockCount; // 80
    public const int ObjectsPerRound = 168;
    public static string ScheduleVersion => SessionConfig.DevelopmentQuickTest
        ? "previous-wall-angle-v5-stratified-40-trials-quick-origin0-v1"
        : "previous-wall-angle-v6-stratified-40-trials-one-practice";
    public const string TargetIdentityPolicy = "matched-counts-shuffled-24-plus-16-v1";
    public const string HeightAssignmentPolicy = "unbalanced-floor-scatter-pending-reference-v1";
    const int k_AngleCategories = 5;
    const int k_PlaneCount = 8;
    const int k_ObjectsPerPlane = ObjectsPerRound / k_PlaneCount;
    // Optional short test run: 0 means use all 80 trials. A shorter run will not
    // necessarily finish both blocks or preserve the completed-trial angle balance.
    public static int DebugRoundCountOverride { get; set; }

    // How many measured trials the game will actually run. Clamp keeps a debug
    // request inside 1–80; it does not change the full plan generated below.
    public static int RoundCount => System.Math.Min(SessionConfig.DevelopmentQuickTest ? RoundsPerBlock : TotalRounds,
        DebugRoundCountOverride > 0
            ? Mathf.Clamp(DebugRoundCountOverride, 1, TotalRounds)
            : TotalRounds);

    static readonly string[] Shapes = { "Sphere", "Cube", "Pyramid", "Cylinder", "Star", "Capsule" };
    static readonly string[] ColorNames = { "Red", "Blue", "Yellow", "Green" };
    static readonly Color[] ColorValues =
    {
        new Color(0.9f, 0.15f, 0.15f, 1f),
        new Color(0.15f, 0.35f, 0.9f, 1f),
        new Color(0.95f, 0.85f, 0.1f, 1f),
        new Color(0f, 168f / 255f, 120f / 255f, 1f), // Emerald #00A878
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
        public int roundIndex;       // 0-79
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
    static bool s_NeutralQuickTest;
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
            if (s_Rounds == null || s_Participant != SessionConfig.ParticipantId ||
                s_NeutralQuickTest != SessionConfig.DevelopmentQuickTest) Generate();
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
        writer.WriteLine("participant_id,schedule_version,schedule_seed,trial_index,block_index,trial_in_block,voice_condition,angle_pair,target_plane,wall_azimuth_deg,signed_theta_deg,absolute_theta_deg,target_slot,layout_seed,target_shape,target_color,enabled_in_run,start_plane,start_wall_azimuth_deg,theta_reference,target_identity_policy,height_assignment_policy");
        foreach (var round in Rounds)
        {
            // Use the saved voice-block assignment, not whichever voice is speaking
            // right now. Trial/block indices start at 0. % 40 gives position within
            // the block: overall trial index 43 becomes position 3 in block 1.
            string voice = SessionConfig.VoiceBlocksEnabled ? SessionConfig.VoiceLabelForRound(round.roundIndex) : SessionConfig.VoiceTag;
            writer.WriteLine(System.FormattableString.Invariant($"{SessionConfig.ParticipantId},{ScheduleVersion},{ScheduleSeed},{round.roundIndex},{round.blockIndex},{round.roundIndex % RoundsPerBlock},{voice},{round.anglePair},{round.targetPlane},{round.targetPlane * 45},{round.SignedTheta},{round.AbsoluteTheta},{round.targetSlot},{round.layoutSeed},{round.target.shape},{round.target.color},{(round.roundIndex < RoundCount ? 1 : 0)},{round.startPlane},{round.startPlane * 45},previous_target_wall_center,{TargetIdentityPolicy},{HeightAssignmentPolicy}"));
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
    /// Build the single practice trial (practiceIndex 0) using a practice-only seed.
    /// Route its voice through measured round 0, so it follows the assigned first block.
    /// Practice does not consume any of the eighty measured trials or angle quotas.
    /// </summary>
    public static RoundDef PracticeRound(int practiceIndex)
    {
        if (practiceIndex != 0) throw new System.ArgumentOutOfRangeException(nameof(practiceIndex));
        // Practice is excluded from measured round counts and seed sequence.
        var rng = new System.Random(900);
        var target = MakeObj(0, 0);
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
        // Round 0 selects the assigned first voice; IsPractice keeps this unmeasured.
        return new RoundDef { roundIndex = 0,
            blockIndex = 0, target = target, objects = objects,
            startPlane = 0,
            targetPlane = targetIndex / k_ObjectsPerPlane, targetSlot = targetIndex % k_ObjectsPerPlane };
    }

    /// <summary>
    /// Build all eighty measured trials: balance angle categories, choose directions,
    /// shuffle each block, repeat identities with equal counts in both voice blocks,
    /// and place each trial's sole target on its assigned wall.
    /// This chooses wall/slot and a layout seed; RotationalSearchLayout.Build uses
    /// that seed to scatter actual positions and heights within the wall boundaries.
    /// </summary>
    static void Generate()
    {
        s_Participant = SessionConfig.ParticipantId;
        s_NeutralQuickTest = SessionConfig.DevelopmentQuickTest;
        var rng = new System.Random(ScheduleSeed);

        // 1. Make forty angle assignments. These numbers are units of 45°, not degrees:
        //    0, 1, 2, 3, 4 mean 0°, 45°, 90°, 135°, 180°. Repeat eight times for equal counts.
        //    Each assignment gets a pair ID so we can find its mirror after shuffling.
        var magnitudes = new int[RoundsPerBlock];
        for (int i = 0; i < magnitudes.Length; i++) magnitudes[i] = i % k_AngleCategories;
        var turnSteps = new int[RoundsPerBlock];
        // Flip a coin (Next(2) returns 0 or 1): keep the rightward turn or mirror it.
        // Example: magnitude 3 is a 135° right turn; its mirror is step 5 (225°,
        // or 135° left), because 8 - 3 = 5. % 8 wraps wall numbers into 0–7.
        // Zero means remain on the starting wall; 180° mirrors onto itself at step 4.
        for (int i = 0; i < turnSteps.Length; i++)
            turnSteps[i] = magnitudes[i] == 0 || rng.Next(2) == 0 ? magnitudes[i] : (8 - magnitudes[i]) % 8;

        // 2. Randomize each five-trial group separately. Each group contains one of
        // every |theta|, so early trials cannot exhaust the 0°/45° quota in a cluster.
        // Shuffle the eight copies of each magnitude across groups, then use
        // Fisher–Yates within each group. Block two independently orders the same
        // pair IDs; its directions are still mirrored below.
        var pairOrder = new int[TotalRounds];
        for (int block = 0; block < BlockCount; block++)
        {
            int start = block * RoundsPerBlock;
            int groups = RoundsPerBlock / k_AngleCategories;
            for (int magnitude = 0; magnitude < k_AngleCategories; magnitude++)
            {
                var copies = new int[groups];
                for (int copy = 0; copy < groups; copy++) copies[copy] = magnitude + copy * k_AngleCategories;
                Shuffle(copies, rng);
                for (int group = 0; group < groups; group++)
                    pairOrder[start + group * k_AngleCategories + magnitude] = copies[group];
            }
            for (int group = 0; group < groups; group++)
            {
                int offset = start + group * k_AngleCategories;
                for (int i = k_AngleCategories - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (pairOrder[offset + i], pairOrder[offset + j]) = (pairOrder[offset + j], pairOrder[offset + i]);
                }
            }
        }

        // 3. List all 6 shapes × 4 colors = 24 possible targets. Both blocks use
        // all 24 once, plus the same seeded subset of 16 a second time. Each bag
        // is independently shuffled, with no immediate repeat at bag boundaries.
        // Thus both voices have identical identity counts, without claiming 80
        // globally unique targets or pairing exact geometry/identity across voices.
        var allTargets = new List<(int si, int ci)>();
        for (int si = 0; si < Shapes.Length; si++)
            for (int ci = 0; ci < ColorNames.Length; ci++)
                allTargets.Add((si, ci));

        Shuffle(allTargets, rng);
        var repeatedTargets = allTargets.GetRange(0, RoundsPerBlock - allTargets.Count);
        var targetOrder = new List<(int si, int ci)>(TotalRounds);
        for (int block = 0; block < BlockCount; block++)
        {
            AppendTargetBag(targetOrder, allTargets, rng);
            AppendTargetBag(targetOrder, repeatedTargets, rng);
        }

        s_Rounds = new RoundDef[TotalRounds];
        var layoutSeeds = new HashSet<int>();
        // Study trials continue from the final practice target. Quick test skips
        // practice, so anchor it to the centered forward wall instead. Subsequent
        // targets still use the same signed steps, preserving the theta balance.
        int startPlane = SessionConfig.DevelopmentQuickTest ? 0 : PracticeRound(0).targetPlane;

        for (int r = 0; r < TotalRounds; r++)
        {
            // 4. Build this trial's object identities. tsi/tci are the target's
            // shape/color indices into the lookup arrays at the top of this file.
            var (tsi, tci) = targetOrder[r];
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

            // 6. Save the trial. Integer division r / 40 maps indices 0–39 to block 0
            // and 40–79 to block 1. The layout seed controls the separate horizontal
            // position/height draws, so a balanced wall angle need not equal the
            // object's exact bearing. Both the seed and actual positions are logged.
            // Random draws can collide. Retry so every trial really has a distinct
            // layout seed, as promised by the schedule manifest and validator.
            int layoutSeed;
            do { layoutSeed = rng.Next(1, int.MaxValue); } while (!layoutSeeds.Add(layoutSeed));
            s_Rounds[r] = new RoundDef
            {
                roundIndex = r,
                blockIndex = r / RoundsPerBlock,
                target = target,
                objects = objects.ToArray(), startPlane = startPlane, targetPlane = plane, targetSlot = slot,
                layoutSeed = layoutSeed, anglePair = pair
            };
            startPlane = plane;
        }

        Debug.Log($"[ChallengeSet] Generated {TotalRounds} balanced rounds: {ScheduleVersion}, seed={ScheduleSeed}");
    }

    /// <summary>
    /// Shuffle a unique-identity bag and append it to the target sequence. If its
    /// first target equals the previous bag's last target, exchange its first two
    /// entries. A bag has no internal duplicates, so this prevents immediate repeats
    /// without redrawing identities or changing either block's identity counts.
    /// </summary>
    static void AppendTargetBag(List<(int si, int ci)> order, List<(int si, int ci)> source, System.Random rng)
    {
        var bag = new List<(int si, int ci)>(source);
        Shuffle(bag, rng);
        if (order.Count > 0 && order[order.Count - 1] == bag[0])
            (bag[0], bag[1]) = (bag[1], bag[0]);
        order.AddRange(bag);
    }

    /// <summary>
    /// Fisher–Yates shuffle: work backward, choose uniformly among the unplaced
    /// entries (0 through i inclusive), and swap the chosen entry into position i.
    /// Already placed entries stay untouched; every ordering is equally likely.
    /// </summary>
    static void Shuffle<T>(IList<T> values, System.Random rng)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
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
