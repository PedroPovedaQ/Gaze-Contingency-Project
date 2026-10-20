"""Offline guidance evidence and blinded-rating export; never a perceived-accuracy score."""
import argparse
import csv
import hashlib
import json
import statistics
from collections import Counter, defaultdict
from pathlib import Path

from guidance_audit import audit, finite
from mega_csv import replay_rows

VERSION = "guidance-evaluation-v1"
SEQUENCE_VERSION = "guidance-sequences-v1"
# Engineering review threshold, not a validated perceptual/clinical cutoff.
CONFIRMATION_REVIEW_SECONDS = 1.0
RATING_FIELDS = ["rater_id", "appropriateness_1_5", "naturalness_1_5",
                 "timing_1_5", "rating_confidence", "preferred_choice", "notes"]
CSV_FIELDS = ["case_id", "decision_id", "sourceTrialId", "trialId", "time",
              "sequence", "phrase", "response_status", "guidance_state_status",
              "software_response_seconds", "software_playback_seconds",
              "evidence_ids"] + RATING_FIELDS


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def detail(record):
    """Read only structured trace data, not instructions embedded in that data."""
    text = record.get("detail") or ""
    try:
        result = json.loads(text[text.index("{"):])
        return result if isinstance(result, dict) else {}
    except (ValueError, TypeError):
        return {}


def evidence(record):
    return f"seq:{record['sequence']}"


def identity(record):
    parsed = detail(record)
    value = parsed.get("request_id") or parsed.get("decision_id") or record.get("guidanceDecisionId")
    if value:
        return value
    for part in (record.get("detail") or "").split(";"):
        if part.startswith("decision_id="):
            return part.partition("=")[2]
    return None


def stats(values):
    values = sorted(v for v in values if finite(v) and v >= 0)
    # Nearest-rank p95: descriptive software latency, not an acoustic measurement.
    return {"count": len(values), "median": statistics.median(values) if values else None,
            "p95": values[max(0, (95 * len(values) + 99) // 100 - 1)] if values else None}


def check(name, status, ids, explanation):
    return dict(name=name, status=status, evidence_ids=ids, detail=explanation)


def choices(question):
    criteria = question.get("criteria")
    return criteria if isinstance(criteria, dict) else {}


def evaluate_choices(payload, response, ids):
    """Check the declared contract and signed direction, without judging naturalness."""
    checks = []
    questions = payload.get("questions", {})
    answers = response.get("answers", {})
    if not isinstance(answers, dict):
        answers = {}
    state = payload.get("state")
    state = state if isinstance(state, dict) else {}
    runtime = state.get("runtime", {})
    runtime = runtime if isinstance(runtime, dict) else {}
    gaze = runtime.get("evidence", {})
    playback = runtime.get("playback", {})
    gaze = gaze if isinstance(gaze, dict) else {}
    playback = playback if isinstance(playback, dict) else {}
    for name, question in questions.items():
        if not isinstance(question, dict) or question.get("type") != "choice":
            continue
        offered = choices(question)
        answer = answers.get(name)
        selected = answer.get("choice") if isinstance(answer, dict) else None
        membership = ("unknown" if not offered or selected is None else
                      "pass" if isinstance(selected, str) and selected in offered else "fail")
        checks.append(check("choice_membership:" + name, membership, ids,
                            "Exact offered-choice membership; not evidence of the best choice."))
        if name == "navigation_phrase":
            metadata = offered.get(selected, {}) if isinstance(selected, str) else {}
            semantics = metadata.get("semantics", {}) if isinstance(metadata, dict) else {}
            horizontal = semantics.get("horizontal") if isinstance(semantics, dict) else None
            yaw = state.get("relative_yaw_degrees")
            known = (membership == "pass" and state.get("schema") == "speech-navigation-v2"
                     and gaze.get("valid") is True and gaze.get("geometryAvailable") is True
                     and horizontal in ("left", "right") and finite(yaw) and 0 < abs(yaw) < 180)
            status = ("pass" if (yaw < 0) == (horizontal == "left") else "fail") if known else "unknown"
            checks.append(check("direction_sign_consistency", status, ids,
                                "Candidate metadata vs recorded request yaw only; not headset accuracy, "
                                "magnitude wording, subsequent motion, or perceptual correctness."))
        if name == "transmit":
            known = all(isinstance(v, bool) for v in
                        (gaze.get("valid"), playback.get("is_speaking"), playback.get("is_busy")))
            status = "unknown"
            if membership == "pass" and selected == "speak" and known:
                status = "pass" if (gaze["valid"] and not playback["is_speaking"]
                                    and not playback["is_busy"]) else "fail"
            checks.append(check("speak_prerequisites", status, ids,
                                "Recorded gaze-valid and idle-audio prerequisites only; waiting is not "
                                "automatically correct, and useful timing still needs review."))
    return checks


def central_state_status(payload):
    """Recognize the actual versioned state, not legacy booleans as an invented phase."""
    def contains(value):
        if isinstance(value, dict):
            recorded = (value.get("version") == "guidance-state-v1"
                        and isinstance(value.get("phase"), str) and bool(value["phase"].strip()))
            return recorded or any(contains(v) for v in value.values())
        if isinstance(value, list):
            return any(contains(v) for v in value)
        return False
    return "recorded" if contains(payload.get("state")) else "unavailable_legacy_or_missing"


def state_snapshot(state):
    runtime = state.get("runtime") or {}
    return runtime.get("guidanceState") or (runtime.get("evidence") or {}).get("guidanceState") or {}


def event_type(state):
    reason = state.get("reason")
    action = state_snapshot(state).get("allowedAction")
    if reason == "controller_target_gaze_vicinity" or action == "ConfirmPointing":
        return "controller_confirmation"
    if reason == "gaze_target_vicinity_hold" or action == "ConfirmGaze":
        return "gaze_confirmation"
    return "other"


def qualification(state, kind):
    snapshot = state_snapshot(state)
    expected = "ConfirmGaze" if kind == "gaze_confirmation" else "ConfirmPointing"
    if (snapshot.get("version") == "guidance-state-v1" and snapshot.get("phase")
            and snapshot.get("allowedAction") == expected):
        if snapshot.get("gazeValid") is True and (
                kind == "gaze_confirmation" or snapshot.get("controllerValid") is True):
            return "qualified"
    gaze = state.get("runtime", {}).get("evidence", {})
    fields = (["valid", "targetGazeQualified"] if kind == "gaze_confirmation" else
              ["valid", "controllerEvidenceValid", "controllerOnTarget",
               "controllerTargetHoldQualified", "gazeWithinTargetVicinity"])
    values = [gaze.get(key) for key in fields]
    if any(v is False for v in values):
        return "unqualified"
    return "qualified" if all(v is True for v in values) else "unknown"


def add_sequence_context(decisions, replay):
    """Retrospective event episodes; never feed future evidence into benchmark inputs.

    Start at the first *recorded qualified request*, not guessed sensor onset. Only
    exact decision-ID playback closes an episode successfully. Recorded invalidity,
    disqualification, lifecycle boundaries and missing sample intervals break chains.
    Sparse legacy evidence cannot establish continuous gaze qualification.
    """
    by_sequence = {row["sequence"]: row for row in decisions}
    ambiguous = {row["decision_id"] for row in decisions if any(
        c["name"] == "unique_request_identity" and c["status"] == "fail" for c in row["checks"])}
    by_id = {row["decision_id"]: row for row in decisions if row["decision_id"] not in ambiguous}
    active, episodes, history = {}, [], defaultdict(list)
    last_sample = None
    current_trial = None

    def finish(kind, outcome, record=None):
        episode, members = active.pop(kind)
        episode["outcome"] = outcome
        if record:
            episode["evidence_ids"].append(evidence(record))
        delay = (record["time"] - episode["first_qualified_request_time"]
                 if outcome == "played" else None)
        episode["qualification_to_playback_seconds"] = delay
        episode["latency_review"] = ("unknown" if delay is None else
            "over_budget" if delay > CONFIRMATION_REVIEW_SECONDS else "within_budget")
        episode["evidence_ids"] = sorted(set(episode["evidence_ids"]))
        for row in members:
            row["sequence_context"]["confirmation_episode"] = episode
        episodes.append(episode)

    for record in replay:
        trial = record.get("sourceTrialId") or record.get("trialId")
        if trial != current_trial:
            for kind in list(active):
                finish(kind, "trial_changed", record)
            current_trial, last_sample = trial, None
        kind = record.get("kind")
        if last_sample is not None and record["time"] - last_sample > .25:
            for key in list(active):
                finish(key, "observation_gap", record)
        boundary = kind in ("search_paused", "application_paused", "selection", "trial",
                            "transition", "end")
        if boundary or (kind == "sample" and (record.get("gazeAvailable") is False
                                             or record.get("gazeTracked") == 0)):
            for key in list(active):
                finish(key, "lifecycle_boundary" if boundary else "invalid_gaze", record)
        if kind == "sample":
            last_sample = record["time"]
            snapshot = record.get("guidanceState") or {}
            if snapshot.get("version") == "guidance-state-v1" and snapshot.get("phase"):
                target = snapshot.get("targetObjectId")
                for key in list(active):
                    controller = key == "controller_confirmation"
                    invalid = (snapshot.get("gazeValid") is False or
                               controller and snapshot.get("controllerValid") is False)
                    departed = (target and (
                        snapshot.get("gazeValid") is True and "vicinityWinnerObjectId" in snapshot
                        and snapshot["vicinityWinnerObjectId"] != target or
                        controller and snapshot.get("controllerValid") is True
                        and "controllerHitObjectId" in snapshot and snapshot["controllerHitObjectId"] != target))
                    if invalid or departed:
                        finish(key, "invalid_gaze" if invalid else "disqualified", record)
        row = by_sequence.get(record["sequence"])
        if row:
            state = row["request_payload"].get("state", {})
            state = state if isinstance(state, dict) else {}
            if row["kind"] == "local":
                local = record.get("guidanceDecision") or {}
                state = {"reason": local.get("reason"), "runtime": {
                    "guidanceState": local.get("guidanceState") or {}}}
            row["event_type"] = event_type(state)
            row["qualification_status"] = (qualification(state, row["event_type"])
                if row["event_type"] != "other" else "unknown")
            row["sequence_context"] = {
                "version": SEQUENCE_VERSION,
                "recent_playback": [h for h in history[trial][-6:]
                                    if record["time"] - h["time"] <= 15],
                "confirmation_episode": None}
            if row["decision_id"] in ambiguous:
                row["qualification_status"] = "unknown"
        if identity(record) in ambiguous:
            for key in list(active):
                finish(key, "ambiguous_identity", record)
            continue
        if row:
            for key in list(active):
                if qualification(state, key) == "unqualified":
                    finish(key, "disqualified", record)
            event = row["event_type"]
            if row["qualification_status"] == "qualified":
                if event not in active:
                    active[event] = (dict(
                        event_type=event, first_qualified_request_time=row["time"],
                        qualification_basis="first recorded qualified request; sensor onset unknown",
                        continuity="observed request qualification only; not proof of continuous fixation",
                        review_budget_seconds=CONFIRMATION_REVIEW_SECONDS,
                        wait_count=0, attempts=[], evidence_ids=[]), [])
                episode, members = active[event]
                answer = row["answers"].get("transmit", {}).get("choice")
                playback = state.get("runtime", {}).get("playback", {})
                idle = (not playback["is_speaking"] and not playback["is_busy"]
                        if all(isinstance(playback.get(k), bool)
                               for k in ("is_speaking", "is_busy")) else None)
                episode["wait_count"] += int(answer == "wait")
                episode["attempts"].append(dict(
                    time=row["time"], phrase=row["phrase"], transmit=answer,
                    audio_idle=idle,
                    response_status=row["response_status"],
                    software_playback_seconds=row["software_playback_seconds"],
                    evidence_ids=row["evidence_ids"]))
                episode["evidence_ids"].extend(row["evidence_ids"])
                members.append(row)
        if kind == "audio_playback_start":
            owner = by_id.get(identity(record))
            event = owner.get("event_type", "other") if owner else "other"
            if event in active and owner in active[event][1]:
                finish(event, "played", record)
            history[trial].append(dict(time=record["time"], phrase=record.get("text"),
                event_type=event, evidence_ids=[evidence(record)]))
    for key in list(active):
        finish(key, "recording_ended")
    return episodes


def evaluate_recording(recording_path, output_directory):
    """Write three local artifacts and return the report. Never call a model or network.

    Replay-relative event times are the only operands of latency differences.
    The benchmark contains exactly the recorded request payload, never later evidence.
    Existing human rating columns are preserved by case ID on regeneration.
    """
    source = Path(recording_path)
    warnings = []
    replay = [row for _, row in replay_rows(source, warnings)]
    checksum = hashlib.sha256(source.read_bytes()).hexdigest()
    header = replay[0]
    provenance = dict(recording_sha256=checksum, recording_id=header.get("recordingId"),
                      app_version=header.get("appVersion"), build_guid=header.get("buildGuid"),
                      evaluator_version=VERSION)
    groups = defaultdict(list)
    for record in replay:
        key = identity(record)
        if key:
            groups[key].append(record)
    decisions, benchmark = [], []
    for key, records in groups.items():
        requests = [r for r in records if r.get("kind") == "jev_trace"
                    and detail(r).get("stage") == "request"]
        local = [r for r in records if r.get("kind") == "guidance_decision"
                 and (r.get("guidanceDecision") or {}).get("stage") == "requested"]
        # Suspension samples and orphan completions are audit evidence, not rating decisions.
        if not requests and not local:
            continue
        request = (requests or local)[0]
        request_trace = detail(request)
        payload = request_trace.get("payload", {}) if requests else {}
        payload = payload if isinstance(payload, dict) else {}
        valid_payload = isinstance(payload.get("questions"), dict) and bool(payload["questions"])
        responses = [r for r in records if r.get("kind") == "jev_trace"
                     and detail(r).get("stage") == "response" and r["sequence"] > request["sequence"]]
        response_record = responses[0] if responses else None
        response = detail(response_record) if response_record else {}
        starts = [r for r in records if r.get("kind") == "audio_playback_start"]
        start = starts[0] if starts else None
        ids = [evidence(r) for r in [request, response_record, start] if r]
        case_id = checksum[:16] + ":" + str(request["sequence"])
        checks = evaluate_choices(payload, response, ids) if valid_payload else []
        if len(requests) > 1:
            checks.append(check("unique_request_identity", "fail", [evidence(r) for r in requests],
                                "Duplicate ID is ambiguous; exclude from benchmark and latency summaries."))
        response_seconds = response_record["time"] - request["time"] if response_record else None
        playback_seconds = start["time"] - request["time"] if start else None
        if len(requests) > 1:
            response_seconds = playback_seconds = None
        if finite(playback_seconds) and playback_seconds < 0:
            checks.append(check("playback_time_order", "fail", ids, "Playback precedes request."))
            playback_seconds = None
        answers = response.get("answers")
        answers = answers if isinstance(answers, dict) else {}
        state = payload.get("state")
        state = state if isinstance(state, dict) else {}
        selected = next((answers[n].get("choice") for n in ("navigation_phrase", "round_encouragement")
                         if isinstance(answers.get(n), dict)), None)
        phrase = selected or state.get("proposed_phrase") or (
            (local[0].get("guidanceDecision") or {}).get("phrase") if local else None)
        row = dict(decision_id=key, case_id=case_id, sourceTrialId=request.get("sourceTrialId"),
                   trialId=request.get("trialId"), time=request["time"], sequence=request["sequence"],
                   round=request.get("round"), block=request.get("block"),
                   kind="jev_request" if requests else "local", phrase=phrase,
                   played_phrase=(start.get("text") or None) if start else None,
                   playback_status="recorded_start" if start else "not_observed",
                   response_status=response.get("status", "missing" if requests else "local_no_model"),
                   request_payload=payload, answers=answers, checks=checks,
                   software_response_seconds=response_seconds, software_playback_seconds=playback_seconds,
                   guidance_state_status=central_state_status(payload if requests else
                       {"state": (request.get("guidanceDecision") or {}).get("guidanceState")}), evidence_ids=ids)
        decisions.append(row)
        if requests and valid_payload and len(requests) == 1:
            participant = request.get("participantCode") or ""
            needs_mapping = not participant or participant.upper().startswith(("DEV", "TEST", "DEMO"))
            session = header.get("recordingId") or checksum
            trial = request.get("sourceTrialId") or request.get("trialId")
            benchmark.append(dict(schema="guidance-benchmark-v1", case_id=case_id,
                source=dict(recording_sha256=checksum, recording_id=header.get("recordingId"),
                            request_sequence=request["sequence"], request_time_seconds=request["time"]),
                group=dict(participant_group=digest("participant:" + participant) if not needs_mapping else None,
                           session_group=digest("session:" + session),
                           trial_group=digest("trial:" + session + ":" + trial) if trial else None,
                           requires_participant_mapping=needs_mapping),
                input=dict(payload=payload), evidence_ids=[evidence(request)]))
    decisions.sort(key=lambda row: row["sequence"])
    episodes = add_sequence_context(decisions, replay)
    # Reuse the contract auditor. Missing raw-eye input is unavailable evidence, never a failed sensor.
    consistency = audit({}, replay)
    missing_eye = [i for i in consistency["issues"] if i["code"] == "missing_exact_eye_observation"]
    integrity = [i for i in consistency["issues"] if i["code"] != "missing_exact_eye_observation"]
    def counts(prefix):
        result = Counter(c["status"] for row in decisions for c in row["checks"] if c["name"].startswith(prefix))
        return {status: result[status] for status in ("pass", "fail", "unknown")}
    summary = dict(jev_requests=sum(r["kind"] == "jev_request" for r in decisions),
                   benchmark_cases=len(benchmark), local_decisions=sum(r["kind"] == "local" for r in decisions),
                   human_rating_rows=len(decisions),
                   central_state_requests=sum(r["guidance_state_status"] == "recorded" for r in decisions),
                   choice_checks=counts("choice_membership"), geometry_checks=counts("direction_sign"),
                   request_to_response_seconds=stats(r["software_response_seconds"] for r in decisions),
                   request_to_playback_seconds=stats(r["software_playback_seconds"] for r in decisions),
                   jev_request_to_playback_seconds=stats(r["software_playback_seconds"] for r in decisions
                                                        if r["kind"] == "jev_request"),
                   local_request_to_playback_seconds=stats(r["software_playback_seconds"] for r in decisions
                                                          if r["kind"] == "local"),
                   response_status_counts=dict(Counter(r["response_status"] for r in decisions
                                                       if r["kind"] == "jev_request")),
                   confirmation_sequence_count=len(episodes),
                   confirmation_latency_reviews=dict(Counter(e["latency_review"] for e in episodes)),
                   qualification_to_playback_seconds=stats(
                       e["qualification_to_playback_seconds"] for e in episodes),
                   raw_eye_checks_unavailable=len(missing_eye), recording_consistency_issues=len(integrity))
    provenance["sequence_evaluator_version"] = SEQUENCE_VERSION
    report = dict(schema=VERSION, provenance=provenance, summary=summary, decisions=decisions,
                  confirmation_sequences=episodes,
                  consistency=dict(contract_supported=consistency["contract"], issues=integrity,
                                   raw_eye_status="not_supplied_use_guidance_audit_for_exact_observation_checks"),
                  warnings=warnings, replay_complete=consistency["replay_complete"],
                  limitations=[
                      "No human ratings inferred. Perceived correctness, naturalness and timing require review.",
                      "Jev output is an observation, never ground truth; choice membership is not accuracy.",
                      "Geometry checks compare recorded request values, not independently measured gaze accuracy.",
                      "All latency differences use replay event time; acoustic onset and end-to-end hardware latency are unmeasured.",
                      "Absent or legacy state remains unknown. Missing playback does not prove failure.",
                      "Benchmark inputs are frozen requests; later response and playback appear only in this retrospective report.",
                      "Split by participant and session before training; DEV/test identities require manual participant mapping.",
                      "Artifacts are local research data; pseudonym hashes are not anonymization."])
    out = Path(output_directory)
    out.mkdir(parents=True, exist_ok=True)
    ratings_path = out / "guidance-human-ratings.csv"
    previous = {}
    if ratings_path.exists():
        with ratings_path.open(newline="", encoding="utf-8") as stream:
            previous = {r["case_id"]: r for r in csv.DictReader(stream)}
        unmatched = set(previous) - {row["case_id"] for row in decisions}
        if any(any(previous[key].get(field) for field in RATING_FIELDS) for key in unmatched):
            raise ValueError("Existing ratings belong to another recording/version; use a new output directory.")
    (out / "guidance-evaluation.json").write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    (out / "guidance-benchmark.jsonl").write_text(
        "".join(json.dumps(case, allow_nan=False) + "\n" for case in benchmark), encoding="utf-8")
    with ratings_path.open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=CSV_FIELDS)
        writer.writeheader()
        for row in decisions:
            exported = {field: row.get(field, "") for field in CSV_FIELDS}
            exported["evidence_ids"] = ";".join(row["evidence_ids"])
            exported.update({field: previous.get(row["case_id"], {}).get(field, "") for field in RATING_FIELDS})
            writer.writerow(exported)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--recording", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    report = evaluate_recording(args.recording, args.out)
    print(json.dumps(report["summary"], indent=2))


if __name__ == "__main__":
    main()
