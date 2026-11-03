"""Makes a Thunderstore package (a zip ready to upload) for each mod.

    python Thunderstore/pack.py                 every mod in mods.json
    python Thunderstore/pack.py SongPlayer ...  just the mods named (folder names)

For each mod it does a clean Release build, then writes Thunderstore/bin/<team>-<name>-<version>.zip
holding:
  manifest.json  name, version, description and the BepInEx pack dependency (from mods.json)
  icon.png       the mod's in-game pixel-art icon (<Mod>/Icon.cs), scaled up to 256 x 256
  README.md      the mod's part of the repo README, plus install, icon and settings notes
  CHANGELOG.md   Thunderstore/changelogs/<Mod>.md
  <Mod>.dll      mod managers put it in BepInEx/plugins/<team>-<name>/

Nothing is uploaded. Sign in at thunderstore.io and upload the zips by hand.

Before packaging a new version: bump the version in the mod's .csproj and Plugin.cs, and add a
"## <version>" entry to its changelog. The script stops if they don't match. It also stops if your
Windows user name turns up in any file it would publish.
"""

import json
import math
import re
import struct
import subprocess
import sys
import zipfile
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
OUT = HERE / "bin"


class PackError(Exception):
    pass


def read(path):
    return path.read_text(encoding="utf-8")


# ---------------------------------------------------------------- the mod's own files

def version_of(folder):
    csproj = read(ROOT / folder / f"{folder}.csproj")
    m = re.search(r"<Version>([^<]+)</Version>", csproj)
    if not m:
        raise PackError(f"{folder}.csproj has no <Version>")
    version = m.group(1).strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+", version):
        raise PackError(f"{folder}: Thunderstore needs a version like 1.2.3, not {version}")

    plugin = read(ROOT / folder / "Plugin.cs")
    m = re.search(r'PluginVersion\s*=\s*"([^"]+)"', plugin)
    if not m or m.group(1) != version:
        raise PackError(f"{folder}: Plugin.cs PluginVersion doesn't match the .csproj version {version}")
    return version


def plugin_info(folder):
    plugin = read(ROOT / folder / "Plugin.cs")
    name = re.search(r'PluginName\s*=\s*"([^"]+)"', plugin).group(1)
    guid = re.search(r'PluginGuid\s*=\s*"([^"]+)"', plugin).group(1)
    return name, guid


def build(folder):
    # --no-incremental so DebugType=none really applies. A DLL left over from an ordinary Release
    # build would still hold the path to its PDB, which includes your user folder.
    csproj = ROOT / folder / f"{folder}.csproj"
    result = subprocess.run(
        ["dotnet", "build", str(csproj), "-c", "Release", "-p:DebugType=none", "--no-incremental",
         "-nologo", "-v:quiet"],
        capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr)
        raise PackError(f"{folder}: the build failed")
    dll = ROOT / folder / "bin" / "Release" / f"{folder}.dll"
    if not dll.exists():
        raise PackError(f"{folder}: the build didn't make {dll}")
    return dll


def uncommitted(folder):
    result = subprocess.run(["git", "status", "--porcelain", "--", folder, "Shared"],
                            cwd=ROOT, capture_output=True, text=True)
    return result.stdout.strip()


# ---------------------------------------------------------------- icon.png

ICON_SIZE = 256
ICON_SCALE = 14                                  # 16 pixels x 14 = 224, leaving a 16 pixel border
ICON_OFFSET = (ICON_SIZE - 16 * ICON_SCALE) // 2
BACKGROUND = (52, 48, 64)                        # a little lighter than the icon row's buttons, so outlines show
EDGE = (88, 83, 104)
EDGE_WIDTH = 4
CORNER = 40


def palette():
    source = read(ROOT / "Shared" / "IconBar.cs")
    found = re.findall(r"\{\s*'(.)',\s*new Color32\((\d+),\s*(\d+),\s*(\d+),\s*(\d+)\)\s*\}", source)
    if not found:
        raise PackError("couldn't find the icon palette in Shared/IconBar.cs")
    return {letter: tuple(int(v) for v in rgba) for letter, *rgba in found}


def icon_pixels(folder):
    source = read(ROOT / folder / "Icon.cs")
    rows = re.findall(r'"([^"]*)"', source[source.index("Pixels"):])[:16]
    if len(rows) != 16 or any(len(r) != 16 for r in rows):
        raise PackError(f"{folder}/Icon.cs: expected 16 rows of 16 letters")
    return rows


def rounded_box_distance(x, y):
    # Signed distance from the pixel centre to the edge of the rounded square (negative inside).
    half = ICON_SIZE / 2
    qx = abs(x + 0.5 - half) - (half - CORNER)
    qy = abs(y + 0.5 - half) - (half - CORNER)
    outside = math.hypot(max(qx, 0), max(qy, 0))
    return outside + min(max(qx, qy), 0) - CORNER


def render_icon(rows, colours):
    pixels = bytearray()
    for y in range(ICON_SIZE):
        pixels.append(0)                         # PNG filter byte: none
        for x in range(ICON_SIZE):
            d = rounded_box_distance(x, y)
            alpha = min(max(0.5 - d, 0.0), 1.0)
            r, g, b = EDGE if d > -EDGE_WIDTH else BACKGROUND
            a = round(alpha * 255)
            ix, iy = x - ICON_OFFSET, y - ICON_OFFSET
            if 0 <= ix < 16 * ICON_SCALE and 0 <= iy < 16 * ICON_SCALE:
                letter = rows[iy // ICON_SCALE][ix // ICON_SCALE]
                if letter in colours:
                    r, g, b, a = colours[letter]
            pixels += bytes((r, g, b, a))
    return png(ICON_SIZE, ICON_SIZE, bytes(pixels))


def png(width, height, filtered_rgba):
    def chunk(kind, data):
        return (struct.pack(">I", len(data)) + kind + data
                + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF))
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(filtered_rgba, 9)) + chunk(b"IEND", b""))


# ---------------------------------------------------------------- README.md

def readme(display, guid, description, website, others):
    lines = read(ROOT / "README.md").splitlines()

    # The mod's row in the README's table: Mod | Version | Open with | What it does | Who needs it
    row = next((l for l in lines if l.startswith(f"| [{display}](")), None)
    if row is None:
        raise PackError(f"README.md: no row for {display} in the mods table")
    cells = [c.strip() for c in row.strip().strip("|").split("|")]
    open_with, who = cells[2], cells[4]

    # The mod's own section, up to the next heading.
    heading = f"### {display}"
    if heading not in lines:
        raise PackError(f"README.md: no '{heading}' section")
    body = []
    for line in lines[lines.index(heading) + 1:]:
        if line.startswith("## ") or line.startswith("### "):
            break
        body.append(line)
    section = "\n".join(body).strip()
    # Thunderstore has no pre-releases, so say what the label means.
    section = section.replace("> **Pre-release:**", "> **Not fully tested:**")

    has_key = re.search(r"\bF\d+\b", open_with) is not None
    fn = " Mac keyboards use the F keys for brightness and volume, so hold **Fn** with the mod's key." \
        if has_key else ""
    other_links = ", ".join(f"[{name}](#{anchor(name)})" for name in others)

    text = f"""# {display}

{description}

| | |
|---|---|
| **Open with** | {open_with} |
| **Who needs it** | {who} |

{section}

## The icon

Like the other mods in this set, {display} puts a small pixel-art icon in a row at the bottom-right of the screen. Click it to do what the mod's key does (or to switch the mod on and off, for mods without a key), and hover over it to see its name and key. A green dot means it's on, and a greyed-out icon means it's off. The row only shows in a world, while no game menu is open.

## Settings

After the first launch, the settings are in `BepInEx/config/{guid}.cfg`. In r2modman or Gale, open them from the config editor. Each setting has a comment explaining it. Restart the game after changing them. `ShowIcon` hides the mod's icon.

## Mac

Install it through [r2modmac](https://github.com/Zard-Studios/r2modmac).{fn} The mods haven't been tried on a Mac yet.

## More mods

Other mods in this set: {other_links}. Source code, and notes on how they've been tested, are on [GitHub]({website}).

This is an unofficial fan project, not made or supported by GigaPuff.
"""
    # Links to other parts of the repo README point at GitHub.
    return re.sub(r"\]\(#", f"]({website}#", text)


def anchor(heading):
    return re.sub(r"[^a-z0-9 -]", "", heading.lower()).replace(" ", "-")


# ---------------------------------------------------------------- checks

def check_private(label, data):
    # Your Windows user name must never be published (it's in the path of your user folder).
    name = Path.home().name.lower()
    if len(name) < 3:
        return
    lowered = data.lower()
    if name.encode("utf-8") in lowered or name.encode("utf-16-le") in lowered:
        raise PackError(f"{label} contains your Windows user name. Nothing was written for this mod.")


def check_manifest(manifest):
    if not re.fullmatch(r"[A-Za-z0-9_]+", manifest["name"]):
        raise PackError(f"package name {manifest['name']!r}: only letters, digits and _ are allowed")
    if len(manifest["description"]) > 250:
        raise PackError(f"{manifest['name']}: the description is {len(manifest['description'])} "
                        "characters, and Thunderstore allows 250")


# ---------------------------------------------------------------- one package

def pack(folder, config, colours):
    mods = config["mods"]
    settings = mods[folder]
    version = version_of(folder)
    display, guid = plugin_info(folder)

    changelog_path = HERE / "changelogs" / f"{folder}.md"
    if not changelog_path.exists():
        raise PackError(f"{folder}: no changelog at Thunderstore/changelogs/{folder}.md")
    changelog = read(changelog_path)
    if f"## {version}" not in changelog.splitlines():
        raise PackError(f"{folder}: the changelog has no '## {version}' entry")

    manifest = {
        "name": settings["name"],
        "version_number": version,
        "website_url": config["website"],
        "description": settings["description"],
        "dependencies": config["dependencies"],
    }
    check_manifest(manifest)

    others = [plugin_info(other)[0] for other in mods if other != folder]
    files = {
        "manifest.json": (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8"),
        "icon.png": render_icon(icon_pixels(folder), colours),
        "README.md": readme(display, guid, settings["description"], config["website"], others).encode("utf-8"),
        "CHANGELOG.md": changelog.encode("utf-8"),
    }

    if uncommitted(folder):
        print(f"  note: {folder} or Shared has changes that aren't committed yet")
    dll = build(folder)
    files[f"{folder}.dll"] = dll.read_bytes()

    for name, data in files.items():
        check_private(f"{folder}: {name}", data)

    OUT.mkdir(exist_ok=True)
    for old in OUT.glob(f"{config['team']}-{settings['name']}-*.zip"):
        old.unlink()
    zip_path = OUT / f"{config['team']}-{settings['name']}-{version}.zip"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        for name, data in files.items():
            z.writestr(name, data)
    return zip_path


def main(args):
    config = json.loads(read(HERE / "mods.json"))
    folders = args or list(config["mods"])
    unknown = [f for f in folders if f not in config["mods"]]
    if unknown:
        print(f"Not in Thunderstore/mods.json: {', '.join(unknown)}")
        return 1

    colours = palette()
    made = []
    for folder in folders:
        print(f"{folder}...")
        try:
            made.append(pack(folder, config, colours))
        except PackError as e:
            print(f"  STOPPED: {e}")
            return 1

    print()
    for path in made:
        print(f"  {path.relative_to(ROOT)}  ({path.stat().st_size // 1024} KB)")
    print(f"\n{len(made)} package(s) ready. Upload them at https://thunderstore.io/package/create/ (pick On-Together)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
