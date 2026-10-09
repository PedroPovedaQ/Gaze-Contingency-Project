"""Explicit unweighted NASA Raw TLX mean; never guess historical scale anchors."""
import math

DIMENSIONS = ("mental", "physical", "temporal", "performance", "effort", "frustration")
METHOD = "raw_tlx_six_dimension_mean_0_100_v1"


def score_tlx(response: dict) -> dict:
    result = dict(nasa_tlx_score=None, nasa_tlx_scoring_method=METHOD,
                  nasa_tlx_scoring_status="missing_scale_metadata")
    try:
        minimum, maximum = float(response.get("scale_min")), float(response.get("scale_max"))
    except (TypeError, ValueError):
        return result
    if minimum != 0 or maximum != 100:
        result["nasa_tlx_scoring_status"] = "unsupported_scale"
        return result
    direction = response.get("performance_direction")
    if direction not in ("high_is_poor", "high_is_good"):
        result["nasa_tlx_scoring_status"] = "missing_performance_direction"
        return result
    try:
        values = [float(response[key]) for key in DIMENSIONS]
    except (KeyError, TypeError, ValueError):
        result["nasa_tlx_scoring_status"] = "missing_or_invalid_dimension"
        return result
    if any(not math.isfinite(v) or not 0 <= v <= 100 for v in values):
        result["nasa_tlx_scoring_status"] = "missing_or_invalid_dimension"
        return result
    if direction == "high_is_good":
        values[3] = 100-values[3]
    result.update(nasa_tlx_score=sum(values)/6,
                  nasa_tlx_scoring_status="scored_all_six_dimensions",
                  nasa_tlx_performance_direction=direction)
    return result
