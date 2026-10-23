"""Paired synthetic requests built by the production C# serializer.

HTTP latency only, never playback/acoustic latency or participant helpfulness.
Each expected speak/wait value describes an explicit validity/pacing contract.
Any offered directional wording is accepted; no exact-phrase preference scoring.
"""
import argparse
import http.client
import json
import time
from pathlib import Path
from guidance_action_pilot import stats

ARMS = ("baseline", "compact", "compact_memory")


def run(fixtures, key, output, repeats=2):
    output.mkdir(parents=True, exist_ok=False)
    (output / "fixtures.json").write_text(json.dumps(fixtures, indent=2) + "\n")
    connection = http.client.HTTPSConnection("ai-gateway.vercel.sh", timeout=3)
    rows = []
    try:
        for repeat in range(repeats):
            for index, case in enumerate(fixtures):
                shift = (index + repeat) % len(ARMS)
                for arm in ARMS[shift:] + ARMS[:shift]:
                    payload = case["payloads"][arm]
                    body = json.dumps(payload, separators=(",", ":"), ensure_ascii=False).encode()
                    row = dict(case=case["id"], repeat=repeat, arm=arm, request_bytes=len(body))
                    started = time.perf_counter()
                    try:
                        connection.request("POST", "/typesafe/v1/systemone", body, {
                            "Authorization": "Bearer " + key, "Content-Type": "application/json"})
                        response = connection.getresponse()
                        raw = response.read(65537)
                        row["seconds"] = time.perf_counter() - started
                        if response.status != 200 or len(raw) > 65536:
                            raise ValueError("http_or_size_failure")
                        answer = json.loads(raw)["answers"]
                        choice = answer["navigation_phrase"]["choice"]
                        transmit = answer["transmit"]["choice"]
                        if (answer["navigation_phrase"]["type"] != "choice" or
                                answer["transmit"]["type"] != "choice" or
                                choice not in payload["questions"]["navigation_phrase"]["criteria"] or
                                transmit not in ("speak", "wait")):
                            raise ValueError("invalid_answer")
                        row.update(status="ok", choice=choice, transmit=transmit,
                                   contract_match=(transmit == "speak") == case["expected_transmit"])
                    except Exception as exc:
                        row.update(seconds=time.perf_counter() - started, status=type(exc).__name__)
                        connection.close()
                        connection = http.client.HTTPSConnection("ai-gateway.vercel.sh", timeout=3)
                    rows.append(row)
                    with (output / "requests.jsonl").open("a") as stream:
                        stream.write(json.dumps(row) + "\n")
                print(f"repeat {repeat + 1}: {case['id']}", flush=True)
    finally:
        connection.close()
    summary = {}
    for arm in ARMS:
        selected = [r for r in rows if r["arm"] == arm]
        ok = [r for r in selected if r["status"] == "ok"]
        summary[arm] = dict(requests=len(selected), successful=len(ok),
                            bytes=stats([r["request_bytes"] for r in selected]),
                            http_seconds=stats([r["seconds"] for r in ok]),
                            beyond_runtime_450ms=sum(r["seconds"] > .45 for r in selected),
                            contract_matches=sum(r["contract_match"] for r in ok))
    result = dict(schema="production-payload-pilot-v1", summary=summary,
                  basis="Synthetic C# production payloads. Real Mac HTTP, no Unity playback or wearer behavior.",
                  limits=f"{len(fixtures) * len(ARMS) * repeats} requests; 3s probe timeout, 450ms runtime threshold reported separately.")
    (output / "summary.json").write_text(json.dumps(result, indent=2) + "\n")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--fixtures", type=Path, required=True)
    parser.add_argument("--keys", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = run(json.loads(args.fixtures.read_text()), json.loads(args.keys.read_text())["jev_key"], args.output)
    print(json.dumps(result, indent=2))
