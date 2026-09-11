"""Descriptive QA exports for the two-block design; no confirmatory inference."""
import pandas as pd


def voice_block_reports(rows):
    data = rows[rows["schema_version"] >= 2].copy()
    if data.empty:
        return pd.DataFrame(), pd.DataFrame()
    data["voice_factor"] = data["voice_condition"].map(
        lambda v: "neutral" if v.startswith("neutral-") else v
    )
    valid = data[(data["outcome"] == "completed") & (data["time_to_find"] >= 0)]
    blocks = valid.groupby(
        ["participant_id", "run_number", "block", "voice_factor", "voice_condition", "voice_order"],
        as_index=False,
    ).agg(trials=("time_to_find", "size"), search_seconds=("time_to_find", "mean"),
          wrong_captures=("wrong_captures", "sum"))
    # Pair within a completed run only; a restart is a different run, never a replacement block.
    complete = data.groupby(["participant_id", "run_number"]).filter(
        lambda group: len(group) == 14 and (group["outcome"] == "completed").all()
        and (group["session_outcome"] == "completed").all()
    )
    means = complete.groupby(["participant_id", "run_number", "voice_factor"])["time_to_find"].mean().unstack()
    if {"neutral", "selfsimilar"}.issubset(means.columns):
        means = means.dropna(subset=["neutral", "selfsimilar"])
        means["self_minus_neutral_seconds"] = means["selfsimilar"] - means["neutral"]
        return blocks, means.reset_index()
    return blocks, pd.DataFrame()


def pairing_exclusions(rows):
    excluded = []
    for (participant, run), group in rows[rows["schema_version"] >= 2].groupby(["participant_id", "run_number"]):
        reasons = []
        if len(group) != 14:
            reasons.append(f"expected 14 trials; found {len(group)}")
        if not (group["outcome"] == "completed").all():
            reasons.append("one or more trials incomplete")
        if not (group["session_outcome"] == "completed").all():
            reasons.append("session incomplete")
        if reasons:
            excluded.append(dict(participant_id=participant, run_number=run, reason="; ".join(reasons)))
    return pd.DataFrame(excluded, columns=["participant_id", "run_number", "reason"])


def write_voice_block_reports(rows, output):
    blocks, pairs = voice_block_reports(rows)
    blocks.to_csv(output / "voice_block_metrics.csv", index=False)
    if pairs.empty:
        pairs = pd.DataFrame(columns=["participant_id", "run_number", "neutral", "selfsimilar", "self_minus_neutral_seconds"])
    pairs.to_csv(output / "voice_pair_differences.csv", index=False)
    exclusions = pairing_exclusions(rows)
    exclusions.to_csv(output / "voice_pair_exclusions.csv", index=False)
    for row in exclusions.itertuples():
        print(f"Voice pairing excluded {row.participant_id} run {row.run_number}: {row.reason}")
