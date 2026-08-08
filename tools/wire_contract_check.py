#!/usr/bin/env python3
"""
Checks the Godot client's hand-written wire models against what the server
actually sends.

The client and the backend deliberately share no code, so `scripts/Net/ApiModels.cs`
is an independent transcription of the JSON contract. That decoupling has one
failure mode: a property renamed on the server deserializes to null on the client
instead of failing loudly. This walks every endpoint's real response against the
client's declared records and reports anything the client expects but does not get.

Usage:
    python3 tools/wire_contract_check.py [base_url]

Exits non-zero if the contract has drifted.
"""

import json
import re
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

MODELS = Path(__file__).resolve().parent.parent / "scripts" / "Net" / "ApiModels.cs"
BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5147/api/v1").rstrip("/")

PRIMITIVES = {
    "string", "int", "long", "bool", "double", "decimal", "float",
    "DateTimeOffset", "TimeSpan", "Guid",
}

RECORD_RE = re.compile(r"public sealed record (\w+)\(([^;]*?)\);", re.S)


def split_params(text):
    """Split a record's parameter list on top-level commas only."""
    parts, depth, current = [], 0, ""
    for ch in text:
        if ch in "<(":
            depth += 1
        elif ch in ">)":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += ch
    if current.strip():
        parts.append(current)
    return [p.strip() for p in parts if p.strip()]


def parse_models():
    """record name -> [(csharp_type, property_name), ...]"""
    source = MODELS.read_text()
    models = {}
    for name, params in RECORD_RE.findall(source):
        fields = []
        for param in split_params(params):
            param = param.split("=")[0].strip()          # drop defaults
            tokens = param.replace("\n", " ").split()
            if len(tokens) < 2:
                continue
            fields.append((" ".join(tokens[:-1]), tokens[-1]))
        models[name] = fields
    return models


def call(method, path, body=None, token=None):
    req = urllib.request.Request(BASE + path, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    data = json.dumps(body).encode() if body is not None else None
    try:
        with urllib.request.urlopen(req, data, timeout=15) as r:
            return r.status, json.loads(r.read() or "null")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or "null")


def check(models, type_name, value, path, problems, seen):
    """Verify a JSON value against a client-declared type."""
    optional = type_name.endswith("?")
    bare = type_name.rstrip("?").strip()

    if value is None:
        if not optional:
            problems.append(f"{path}: client declares non-nullable {bare}, server sent null")
        return

    list_match = re.fullmatch(r"List<(.+)>", bare)
    if list_match:
        if not isinstance(value, list):
            problems.append(f"{path}: client expects a list, server sent {type(value).__name__}")
            return
        for i, item in enumerate(value[:3]):        # a few items is enough to prove the shape
            check(models, list_match.group(1), item, f"{path}[{i}]", problems, seen)
        return

    if bare in PRIMITIVES or bare not in models:
        return                                       # primitives and enums: presence is what matters

    if not isinstance(value, dict):
        problems.append(f"{path}: client expects object {bare}, server sent {type(value).__name__}")
        return

    for field_type, field_name in models[bare]:
        if field_name not in value:
            problems.append(f"{path}.{field_name}: declared by client ({bare}), absent from server JSON")
            continue
        key = (bare, field_name)
        if key in seen and isinstance(value[field_name], (dict, list)):
            continue
        seen.add(key)
        check(models, field_type, value[field_name], f"{path}.{field_name}", problems, seen)


def main():
    models = parse_models()
    print(f"parsed {len(models)} client records from {MODELS.name}")

    status, auth = call("POST", "/auth/register",
                        {"DeviceId": "wire-check-" + uuid.uuid4().hex, "DisplayName": "Wire Check AB"})
    if status != 200:
        print(f"cannot reach {BASE} (register returned {status}). Is the backend running?")
        return 2
    token = auth["Token"]

    # Put the account into a state where every list actually has rows in it —
    # an empty list proves nothing about the shape of its items.
    call("POST", "/jobs/start", {"JobId": "flip_crate"}, token)
    call("POST", "/market/buy", {"GoodId": "streaming_codes", "Quantity": 1}, token)

    probes = [
        ("AuthResponse", "POST", "/auth/register",
         {"DeviceId": "wire-check-" + uuid.uuid4().hex, "DisplayName": "Wire Check AB"}),
        ("PlayerStateDto", "GET", "/me", None),
        ("JobBoardDto", "GET", "/jobs", None),
        ("MarketQuoteDto", "GET", "/market", None),
        ("InventoryDto", "GET", "/inventory", None),
        ("List<TravelOptionDto>", "GET", "/travel/options", None),
        ("LeaderboardDto", "GET", "/leaderboard?mode=Empire", None),
        ("StatsDto", "GET", "/stats", None),
        ("WardrobeDto", "GET", "/wardrobe", None),
        ("TradeResultDto", "POST", "/market/buy", {"GoodId": "streaming_codes", "Quantity": 1}),
        ("ErrorDto", "POST", "/market/buy", {"GoodId": "nope", "Quantity": 1}),
    ]

    problems = []
    for type_name, method, path, body in probes:
        status, payload = call(method, path, body, token)
        if payload is None:
            problems.append(f"{path}: no body (HTTP {status})")
            continue
        before = len(problems)
        check(models, type_name, payload, type_name, problems, set())
        mark = "ok " if len(problems) == before else "DRIFT"
        print(f"  {mark} {method:<4} {path:<32} -> {type_name}")

    print()
    if problems:
        print(f"{len(problems)} contract problem(s):")
        for problem in problems:
            print("  -", problem)
        return 1

    print("client models match the server's JSON on every probed endpoint.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
