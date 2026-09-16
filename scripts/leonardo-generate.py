#!/usr/bin/env python3
"""Generate images with Leonardo.ai's API, within a token budget, from prompts in a file.

Why this exists. The owner has a monthly token allowance on Leonardo (a Canva employee plan),
wants it used through the API rather than by hand ("that's why api tokens exist"), and set three
rules (2026-09-16): the key lives in the environment variable EVOSIM_LEONARDO_KEY on the machine
and never in the repo; only prompts written for the purpose go up, nothing of the project's
(no picture, no genome, no number derived from one) unless the owner approves that instance;
and a session stops at a budget the owner sets. The survey the uses come from is
logbook/specs/leonardo-survey.md: backdrops, title cards and thumbnails for the videos, never
the skin, which is arithmetic on purpose.

Usage:
    python scripts/leonardo-generate.py design/leonardo-prompts.md --budget 60 [--dry-run]
    python scripts/leonardo-generate.py design/leonardo-prompts.md --budget 60 --only arrival

The prompt file is Markdown. Each prompt is a level-three heading followed by its text; a
heading's slug names the output. Lines starting with "model:", "size:" or "count:" under a heading
set that prompt's model id, width x height and image count. Everything else under the heading
is the prompt. Outputs go under scratch/leonardo/<slug>/, gitignored, with the request and the
response beside each image, so a picture the record ever cites can be traced to its prompt.

Budget. Leonardo returns each generation's cost as apiCreditCost; the script keeps a running
total and refuses to start a generation that could take the session over --budget, on the
estimate the API gave for the previous one of the same shape or, before any, on --estimate
(default 20 per image). The total spent is printed at the end and appended to
scratch/leonardo/ledger.tsv. Nothing here retries a failed generation on its own.

No key: the script says so and exits 2. --dry-run prints what would be sent and sends nothing.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

API = "https://cloud.leonardo.ai/api/rest/v1"
ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "scratch" / "leonardo"
DEFAULT_MODEL = "de7d3faf-762f-48e0-b3b7-9d0ac3a3fcf3"  # Phoenix 1.0, per the survey; override per prompt


def parse_prompts(path: Path) -> list[dict]:
    prompts: list[dict] = []
    current: dict | None = None

    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.rstrip()
        if line.startswith("### "):
            title = line[4:].strip()
            slug = re.sub(r"[^a-z0-9]+", "-", title.lower()).strip("-")
            current = {"title": title, "slug": slug, "model": DEFAULT_MODEL,
                       "width": 1280, "height": 720, "count": 1, "lines": []}
            prompts.append(current)
            continue
        if current is None or line.startswith("## ") or line.startswith("# "):
            current = None if (line.startswith("## ") or line.startswith("# ")) else current
            continue
        m = re.match(r"^(model|size|count):\s*(.+)$", line.strip(), re.IGNORECASE)
        if m:
            key, value = m.group(1).lower(), m.group(2).strip()
            if key == "model":
                current["model"] = value
            elif key == "size":
                w, h = value.lower().split("x")
                current["width"], current["height"] = int(w), int(h)
            elif key == "count":
                current["count"] = max(1, min(4, int(value)))
            continue
        current["lines"].append(line)

    for p in prompts:
        p["prompt"] = " ".join(l.strip() for l in p["lines"] if l.strip())
        del p["lines"]

    return [p for p in prompts if p["prompt"]]


def request(method: str, path: str, key: str, body: dict | None = None) -> dict:
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(
        API + path, data=data, method=method,
        headers={"Authorization": f"Bearer {key}", "Accept": "application/json",
                 "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read().decode("utf-8"))


def download(url: str, to: Path) -> None:
    with urllib.request.urlopen(url, timeout=120) as resp, open(to, "wb") as f:
        f.write(resp.read())


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("prompts", type=Path)
    ap.add_argument("--budget", type=float, required=True, help="tokens this session may spend, set by the owner")
    ap.add_argument("--estimate", type=float, default=20.0, help="tokens assumed per image before the API has said")
    ap.add_argument("--only", action="append", default=[], help="slug(s) to run; default every prompt")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    prompts = parse_prompts(args.prompts)
    if args.only:
        prompts = [p for p in prompts if p["slug"] in args.only]
    if not prompts:
        print("no prompts to run", file=sys.stderr)
        return 1

    key = os.environ.get("EVOSIM_LEONARDO_KEY", "").strip()
    if not key and not args.dry_run:
        print("EVOSIM_LEONARDO_KEY is not set; nothing sent (set it in the environment, never in the repo)", file=sys.stderr)
        return 2

    OUT.mkdir(parents=True, exist_ok=True)
    spent = 0.0
    last_cost_per_image = args.estimate
    stamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S")

    for p in prompts:
        body = {
            "prompt": p["prompt"],
            "modelId": p["model"],
            "width": p["width"],
            "height": p["height"],
            "num_images": p["count"],
            "public": False,
        }
        expected = last_cost_per_image * p["count"]

        print(f"== {p['slug']}: {p['count']} x {p['width']}x{p['height']}, expected about {expected:.0f} tokens; spent {spent:.0f} of {args.budget:.0f}")
        if spent + expected > args.budget:
            print("   refused: would pass the budget", file=sys.stderr)
            break
        if args.dry_run:
            print("   dry run; would send:", json.dumps(body, indent=2))
            continue

        folder = OUT / p["slug"]
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{stamp}-request.json").write_text(json.dumps(body, indent=2), encoding="utf-8")

        try:
            started = request("POST", "/generations", key, body)
        except urllib.error.HTTPError as e:
            print(f"   failed: HTTP {e.code} {e.read().decode('utf-8', 'replace')[:300]}", file=sys.stderr)
            break

        job = started.get("sdGenerationJob", {})
        gen_id = job.get("generationId")
        cost = float(job.get("apiCreditCost") or expected)
        spent += cost
        last_cost_per_image = cost / max(1, p["count"])
        print(f"   generation {gen_id}, cost {cost:.0f} tokens, spent {spent:.0f}")

        result = None
        for _ in range(60):
            time.sleep(5)
            result = request("GET", f"/generations/{gen_id}", key).get("generations_by_pk", {})
            if result.get("status") in ("COMPLETE", "FAILED"):
                break

        (folder / f"{stamp}-response.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        if not result or result.get("status") != "COMPLETE":
            print(f"   not complete: {result.get('status') if result else 'no response'}", file=sys.stderr)
            continue

        for i, image in enumerate(result.get("generated_images", [])):
            url = image.get("url")
            if not url:
                continue
            to = folder / f"{stamp}-{i}.png"
            download(url, to)
            print(f"   saved {to.relative_to(ROOT)}")

        with open(OUT / "ledger.tsv", "a", encoding="utf-8") as ledger:
            ledger.write(f"{stamp}\t{p['slug']}\t{gen_id}\t{cost:.0f}\t{spent:.0f}\n")

    print(f"session spent {spent:.0f} tokens of {args.budget:.0f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
