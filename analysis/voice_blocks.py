"""Descriptive QA exports for the two-block design; no confirmatory inference."""
import math
import pandas as pd


def voice_factor(value):
    return 'neutral' if isinstance(value, str) and value.startswith('neutral-') else value


def valid_search_time(value):
    return pd.notna(value) and math.isfinite(value) and value >= 0


def pairing_issues(group):
    """Require the recorded two-block schedule, never assemble blocks from restarts.

    Missing metadata on historical tables retains the old seven-per-block default.
    Current debug runs carry their nominal schedule and are never complete pairs.
    """
    reasons = []
    def count(column, default, minimum=1):
        values = group[column].unique() if column in group else [default]
        if len(values) != 1:
            reasons.append('inconsistent ' + column)
            return None
        try:
            value = float(values[0])
            if not math.isfinite(value) or not value.is_integer() or value < minimum:
                raise ValueError()
            return int(value)
        except (ValueError, TypeError):
            reasons.append('invalid ' + column)
            return None
    per_block = count('rounds_per_block', 7)
    nominal = count('nominal_total_rounds', 2*per_block if per_block else 14)
    enabled = count('total_rounds', nominal or 14)
    debug = count('debug_round_override', 0, minimum=0)
    if debug:
        reasons.append('debug run; not a full study pair')
    if nominal is not None and per_block is not None and nominal != 2*per_block:
        reasons.append('nominal schedule does not contain two equal blocks')
    if enabled is not None and nominal is not None and enabled != nominal:
        reasons.append('enabled count differs from nominal schedule')
    if nominal is not None and len(group) != nominal:
        reasons.append(f'expected {nominal} trials; found {len(group)}')
    if not (group['outcome'] == 'completed').fillna(False).all():
        reasons.append('one or more trials incomplete')
    if not (group['session_outcome'] == 'completed').fillna(False).all():
        reasons.append('session incomplete')
    times = pd.to_numeric(group['time_to_find'], errors='coerce')
    if not times.map(valid_search_time).all():
        reasons.append('missing, negative or non-finite search duration')
    indices = pd.to_numeric(group['round_index'], errors='coerce')
    if nominal is not None and (indices.isna().any() or sorted(indices) != list(range(nominal))):
        reasons.append('missing, duplicate or out-of-range trial indices')
    if group['trial_id'].isna().any() or (group['trial_id'] == '').any() or group['trial_id'].duplicated().any():
        reasons.append('missing or duplicate trial IDs')
    orders = group['voice_order'].unique()
    order = orders[0] if len(orders) == 1 else None
    if not isinstance(order,str) or order not in ('neutral_then_selfsimilar', 'selfsimilar_then_neutral'):
        reasons.append('missing or inconsistent voice order')
    elif per_block is not None:
        for first, label in ((True, 'A'), (False, 'B')):
            block = group[indices < per_block] if first else group[indices >= per_block]
            expected = 'neutral' if first == (order == 'neutral_then_selfsimilar') else 'selfsimilar'
            if len(block) != per_block or not (block['block'] == label).fillna(False).all():
                reasons.append(f'block {label} count or assignment mismatch')
            if not (block['voice_condition'].map(voice_factor) == expected).fillna(False).all() or block['voice_condition'].nunique() != 1:
                reasons.append(f'block {label} voice assignment mismatch')
    return reasons


def voice_block_reports(rows):
    data = rows[rows["schema_version"] >= 2].copy()
    if data.empty:
        return pd.DataFrame(), pd.DataFrame()
    data["voice_factor"] = data["voice_condition"].map(voice_factor)
    data['time_to_find'] = pd.to_numeric(data['time_to_find'], errors='coerce')
    valid = data[(data["outcome"] == "completed") &
                 data['time_to_find'].map(valid_search_time)]
    blocks = valid.groupby(
        ["participant_id", "run_number", "block", "voice_factor", "voice_condition", "voice_order"],
        as_index=False,
    ).agg(trials=("time_to_find", "size"), search_seconds=("time_to_find", "mean"),
          wrong_captures=("wrong_captures", "sum"))
    # Pair within a completed run only; a restart is a different run, never a replacement block.
    complete = data.groupby(["participant_id", "run_number"]).filter(
        lambda group: not pairing_issues(group)
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
        reasons = pairing_issues(group)
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
