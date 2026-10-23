"""Explicit unweighted NASA Raw TLX mean; never guess historical scale anchors."""
import math

DIMENSIONS = ("mental", "physical", "temporal", "performance", "effort", "frustration")
METHOD = "raw_tlx_six_dimension_mean_explicit_scale_v2"


def score_tlx(response: dict) -> dict:
    result = dict(nasa_tlx_score=None, nasa_tlx_score_0_100=None,
                  nasa_tlx_scale_min=None, nasa_tlx_scale_max=None, nasa_tlx_scoring_method=METHOD,
                  nasa_tlx_scoring_status="missing_scale_metadata")
    try:
        minimum, maximum = float(response.get("scale_min")), float(response.get("scale_max"))
    except (TypeError, ValueError):
        return result
    if minimum != 0 or maximum not in (10, 100):
        result["nasa_tlx_scoring_status"] = "unsupported_scale"
        return result
    result.update(nasa_tlx_scale_min=minimum, nasa_tlx_scale_max=maximum,
                  nasa_tlx_scoring_method=f"raw_tlx_six_dimension_mean_0_{int(maximum)}_v2")
    direction = response.get("performance_direction")
    if direction not in ("high_is_poor", "high_is_good"):
        result["nasa_tlx_scoring_status"] = "missing_performance_direction"
        return result
    try:
        values = [float(response[key]) for key in DIMENSIONS]
    except (KeyError, TypeError, ValueError):
        result["nasa_tlx_scoring_status"] = "missing_or_invalid_dimension"
        return result
    if any(not math.isfinite(v) or not 0 <= v <= maximum for v in values):
        result["nasa_tlx_scoring_status"] = "missing_or_invalid_dimension"
        return result
    if direction == "high_is_good":
        values[3] = maximum-values[3]
    result.update(nasa_tlx_score=sum(values)/6,
                  nasa_tlx_score_0_100=sum(values)/6 * (100/maximum),
                  nasa_tlx_scoring_status="scored_all_six_dimensions",
                  nasa_tlx_performance_direction=direction)
    return result
