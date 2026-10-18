"""Validate reports freshly produced by SnowTerrainTests.Measure; no game/save mutation."""
import json
import math
from pathlib import Path
from statistics import median

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "Tools" / "out"
before = json.loads((OUT / "snow-terrain-before.json").read_text(encoding="utf-8-sig"))
after = json.loads((OUT / "snow-terrain-verified.json").read_text(encoding="utf-8-sig"))
runs = after["runs"]
assert len(runs) == 36 and after["length"] == before["length"]
for run in runs:
    assert all(math.isfinite(run[k]) for k in ("distance", "fill", "maxImpact", "airTime"))
    assert run["end"] != "Timeout", run
    assert run["cause"] not in ("HardLanding", "Flip", "Wall", "Valley"), run
    assert run["end"] != "Spilled", run
    assert 30 < run["firstMeaningfulZ"] < 120, run
    first = next(f for f in run["flights"] if f["meaningful"])
    assert first["landed"] and first["landingImpact"] < 6, run
starters = [r for r in runs if r["mode"] == "terrain" and r["level"] == 0]
assert len(starters) == 6 and all(r["longestAirSeconds"] >= 1.2 for r in starters)
max_rocket = [r for r in runs if r["mode"] == "progression" and r["level"] == 30]
assert len(max_rocket) == 2 and all(r["end"] == "Finished" for r in max_rocket)
distances = [median(r["distance"] for r in runs if r["mode"] == "terrain" and r["level"] == n)
             for n in (0, 5, 10, 20, 30)]
assert all(a < b for a, b in zip(distances, distances[1:])), distances

lines = ["PASS: 36 deterministic snow runs, 6 starter flights, safe first landings,",
         "no hard landing/flip/spill/timeout, both max rocket runs finish, increasing upgrade medians.",
         "Actual simulation metres; in-game display is x3.6. Airborne launch and ravine falls excluded.",
         "These are automated players, not a substitute for phone controls/performance testing.", "",
         "mode level player seed | old/new distance m | first flight m | longest s | impact m/s | end/cause"]
for old, new in zip(before["runs"], runs):
    assert all(old[k] == new[k] for k in ("mode", "level", "player", "seed"))
    lines.append(f'{new["mode"]} {new["level"]} {new["player"]} {new["seed"]} | '
                 f'{old["distance"]:.1f}/{new["distance"]:.1f} | {new["firstMeaningfulZ"]:.1f} | '
                 f'{new["longestAirSeconds"]:.2f} | {new["maxImpact"]:.2f} | {new["end"]}/{new["cause"]}')
report = "\n".join(lines) + "\n"
(OUT / "snow-terrain-comparison.txt").write_text(report, encoding="utf-8")
print("\n".join(lines[:6]))
print("Pure-terrain median distances:", [round(x, 1) for x in distances])
