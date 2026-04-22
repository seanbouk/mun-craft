"""
Download Twemoji PNGs for our achievement emoji.

Twemoji is Twitter/X's open-source emoji set (MIT license).
PNGs are 72x72, named by Unicode codepoint.

Usage:
    python scripts/fetch_emoji.py

Downloads to Assets/Resources/Emoji/ with filenames matching
the achievement names (lowercase, underscored) so Unity can
load them via Resources.Load<Texture2D>("Emoji/mud_pie").

To get the FULL Twemoji set (~3,600 PNG files, ~15 MB):
    git clone https://github.com/jdecked/twemoji.git
    cp twemoji/assets/72x72/*.png Assets/Resources/Emoji/
"""

import os
import urllib.request
import sys

# Map: achievement name -> twemoji codepoint filename
# Codepoints from https://github.com/jdecked/twemoji
# Filenames use lowercase hex codepoints separated by hyphens
EMOJI = {
    # Hands
    "mud_pie":       "1f3fa",
    "stone_face":    "1f5ff",
    "snowflake":     "2744",

    # Fire
    "bricks":        "1f9f1",
    "seedling":      "1f331",
    "dewdrop":       "1f4a7",
    "potted_plant":  "1fab4",
    "tea":           "1f375",
    "ice_cube":      "1f9ca",
    "mountain":      "26f0",
    "bell":          "1f514",
    "boulder":       "1faa8",

    # Furnace
    "cottage":       "1f3e0",
    "gem":           "1f48e",
    "scroll":        "1f4dc",
    "crystal_ball":  "1f52e",
    "bucket":        "1faa3",
    "urn":           "26b1",
    "potion":        "1f9ea",
    "bubbles":       "1fae7",

    # Forge
    "monument":      "1f3db",
    "great_tree":    "1f333",
    "foundation":    "1f3d7",
    "snowman":       "2603",
    "world":         "1f30d",
    "summit":        "1f3d4",
    "volcano":       "1f30b",
    "tidal":         "1f30a",
    "bamboo":        "1f38b",
    "sunflower":     "1f33b",

    # Lathe
    "book":          "1f4d6",
    "trophy":        "1f3c6",
    "dice":          "1f3b2",
    "star":          "2b50",
    "bullseye":      "1f3af",
    "circus":        "1f3aa",
    "music_box":     "1f3b5",
    "puzzle":        "1f9e9",

    # Moka Pot
    "matcha":        "1f343",
    "coin":          "1fa99",
    "superstar":     "1f31f",
    "chocolate":     "1f36b",
    "cocktail":      "1f379",
    "cake":          "1f370",
    "gift":          "1f381",
}

CDN_BASE = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72"
OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Emoji")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    total = len(EMOJI)
    done = 0
    failed = 0

    for name, codepoint in EMOJI.items():
        url = f"{CDN_BASE}/{codepoint}.png"
        out_path = os.path.join(OUT_DIR, f"{name}.png")

        if os.path.exists(out_path):
            print(f"  skip {name} (already exists)")
            done += 1
            continue

        try:
            urllib.request.urlretrieve(url, out_path)
            print(f"  [{done+1}/{total}] {name} <- {codepoint}.png")
            done += 1
        except Exception as e:
            print(f"  FAIL {name} ({url}): {e}")
            failed += 1

    print(f"\nDone: {done} downloaded, {failed} failed, {total} total")


if __name__ == "__main__":
    main()
