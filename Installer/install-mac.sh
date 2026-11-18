#!/bin/bash
# Installs r2modmac and gets every SoapieScripts mod for On-Together ready for it.
#
#   1. Installs r2modmac (a Mac mod manager, https://github.com/Zard-Studios/r2modmac)
#      in Applications, unless it's already there.
#   2. Makes SoapieScripts.r2z in your Downloads folder: a profile file listing BepInEx
#      and the newest version of every SoapieScripts mod on Thunderstore.
#   3. Opens r2modmac and shows the file in Finder. Import it in r2modmac, and
#      r2modmac downloads the mods itself.
#
# Quickest way to run it (paste into Terminal):
#   curl -fsSL https://raw.githubusercontent.com/Nelphei/OnTogetherMods/master/Installer/install-mac.sh | bash
#
# Options (when piping from curl, put them after "bash -s --"):
#   --skip "Desk Swarm,Quick Hide"   leave these mods out
#   --no-r2modmac                    don't install r2modmac, only make the profile file
#   --no-open                        don't open r2modmac or Finder at the end
#   --out <folder>                   where to put SoapieScripts.r2z (default: Downloads)

set -eo pipefail

TEAM='SoapieScripts'
COMMUNITY='on-together'
LOADER='BepInEx-BepInExPack'
PROFILE='SoapieScripts'
RELEASE_API='https://api.github.com/repos/Zard-Studios/r2modmac/releases/latest'
AGENT='SoapieScripts-Installer'

SKIP=''
INSTALL_APP=1
OPEN_AT_END=1
OUT_DIR="$HOME/Downloads"
[ -d "$OUT_DIR" ] || OUT_DIR="$HOME"

if [ -t 1 ]; then
    GREEN=$'\033[32m'; YELLOW=$'\033[33m'; RED=$'\033[31m'; CYAN=$'\033[36m'; PLAIN=$'\033[0m'
else
    GREEN=''; YELLOW=''; RED=''; CYAN=''; PLAIN=''
fi
say()  { printf '%s\n' "$*"; }
good() { printf '%s%s%s\n' "$GREEN" "$*" "$PLAIN"; }
warn() { printf '%s%s%s\n' "$YELLOW" "$*" "$PLAIN"; }
die()  { printf '%s%s%s\n' "$RED" "$*" "$PLAIN" >&2; exit 1; }

while [ $# -gt 0 ]; do
    case "$1" in
        --skip)        [ $# -ge 2 ] || die '--skip needs a list of mods.'; SKIP="$2"; shift 2 ;;
        --no-r2modmac) INSTALL_APP=0; shift ;;
        --no-open)     OPEN_AT_END=0; shift ;;
        --out)         [ $# -ge 2 ] || die '--out needs a folder.'; OUT_DIR="$2"; shift 2 ;;
        -h|--help)     say 'Options: --skip "Mod One,Mod Two"  --no-r2modmac  --no-open  --out <folder>'; exit 0 ;;
        *)             die "Unknown option: $1 (try --help)" ;;
    esac
done

[ "$(uname -s)" = 'Darwin' ] || die 'This installer is for Macs. On Windows, use Install.ps1 or Install.cmd.'

TMP=$(mktemp -d "${TMPDIR:-/tmp}/soapiescripts.XXXXXX")
MOUNT=''
cleanup() {
    if [ -n "$MOUNT" ]; then hdiutil detach -quiet "$MOUNT" >/dev/null 2>&1 || true; fi
    rm -rf "$TMP"
}
trap cleanup EXIT

fetch() { curl -fsSL --retry 2 -A "$AGENT" -o "$2" "$1"; }

# Every Mac can run JavaScript through osascript, so it reads the JSON (no Python needed).
cat > "$TMP/helper.js" <<'JS'
ObjC.import('Foundation');

function readJson(path) {
    var text = $.NSString.stringWithContentsOfFileEncodingError(path, $.NSUTF8StringEncoding, null);
    if (text.isNil()) throw new Error('Could not read ' + path);
    return JSON.parse(text.js);
}

function simple(s) { return String(s).replace(/[^A-Za-z0-9]/g, '').toLowerCase(); }

function compareVersions(a, b) {
    var x = String(a).split('.'), y = String(b).split('.');
    for (var i = 0; i < Math.max(x.length, y.length); i++) {
        var d = (parseInt(x[i], 10) || 0) - (parseInt(y[i], 10) || 0);
        if (d !== 0) return d;
    }
    return 0;
}

function newest(pkg) {
    var versions = (pkg.versions || []).filter(function (v) { return v.is_active !== false; });
    versions.sort(function (a, b) { return compareVersions(b.version_number, a.version_number); });
    if (!versions.length) throw new Error(pkg.full_name + ' has no version to install.');
    return versions[0];
}

// release <release.json> <chip>: the dmg for this Mac, as lines: url, sha256 (may be empty), name, tag.
function release(path, chip) {
    var data = readJson(path), assets = data.assets || [];
    var dmg = assets.filter(function (a) { return a.name === 'r2modmac_macos_' + chip + '.dmg'; })[0]
           || assets.filter(function (a) { return /\.dmg$/.test(a.name) && a.name.indexOf(chip) >= 0; })[0];
    if (!dmg) throw new Error('No r2modmac download for this Mac (' + chip + ') in its newest release.');
    var sum = /^sha256:([0-9a-fA-F]{64})$/.exec(dmg.digest || '');
    return [dmg.browser_download_url, sum ? sum[1].toLowerCase() : '', dmg.name, data.tag_name || ''].join('\n');
}

// mods <packages.json> <team> <loader> <skip>: what goes in the profile, each package after
// the ones it needs. Lines: mod<TAB>full name<TAB>version<TAB>shown name,
// skip<TAB>shown name, or warn<TAB>text.
function mods(path, team, loader, skip) {
    var listing = readJson(path), byName = {}, added = {}, out = [];
    listing.forEach(function (p) { byName[p.full_name] = p; });
    var skipNames = String(skip).split(',').map(simple).filter(function (s) { return s.length > 0; });
    function show(p) { return p.full_name === loader ? 'BepInEx' : p.name.replace(/_/g, ' '); }
    function add(p) {
        if (added[p.full_name]) return;
        added[p.full_name] = true;
        var version = newest(p);
        (version.dependencies || []).forEach(function (d) {
            var needed = d.replace(/-[^-]+$/, '');
            if (byName[needed]) add(byName[needed]);
            else out.push('warn\t' + show(p) + ' needs ' + needed + ", which isn't on Thunderstore for On-Together.");
        });
        out.push(['mod', p.full_name, version.version_number, show(p)].join('\t'));
    }
    if (!byName[loader]) throw new Error("Thunderstore doesn't list " + loader + ' for On-Together.');
    add(byName[loader]);
    var ours = listing.filter(function (p) { return p.owner === team && !p.is_deprecated; });
    ours.sort(function (a, b) { return a.name < b.name ? -1 : (a.name > b.name ? 1 : 0); });
    if (!ours.length) throw new Error('Thunderstore lists no ' + team + ' mods for On-Together right now.');
    ours.forEach(function (p) {
        if (skipNames.indexOf(simple(p.name)) >= 0 || skipNames.indexOf(simple(p.full_name)) >= 0) out.push('skip\t' + show(p));
        else add(p);
    });
    return out.join('\n');
}

function run(argv) {
    if (argv[0] === 'release') return release(argv[1], argv[2]);
    if (argv[0] === 'mods') return mods(argv[1], argv[2], argv[3], argv[4] || '');
    throw new Error('Unknown helper command: ' + argv[0]);
}
JS
helper() { osascript -l JavaScript "$TMP/helper.js" "$@"; }

find_app() {
    local folder
    for folder in /Applications "$HOME/Applications"; do
        if [ -d "$folder/r2modmac.app" ]; then printf '%s\n' "$folder/r2modmac.app"; return 0; fi
    done
    return 1
}

install_app() {
    local chip info url sum name tag got source target folder
    chip='x86_64'
    if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" = '1' ]; then chip='aarch64'; fi

    say 'Finding the newest r2modmac on GitHub...'
    fetch "$RELEASE_API" "$TMP/release.json"
    info=$(helper release "$TMP/release.json" "$chip")
    url=$(printf '%s\n' "$info" | sed -n 1p)
    sum=$(printf '%s\n' "$info" | sed -n 2p)
    name=$(printf '%s\n' "$info" | sed -n 3p)
    tag=$(printf '%s\n' "$info" | sed -n 4p)

    say "Downloading $name ($tag)..."
    curl -fL --retry 2 --progress-bar -A "$AGENT" -o "$TMP/$name" "$url"
    if [ -n "$sum" ]; then
        got=$(shasum -a 256 "$TMP/$name" | awk '{print $1}')
        [ "$got" = "$sum" ] || die 'The r2modmac download came out damaged (its checksum is wrong). Run this again.'
    fi

    say 'Installing r2modmac...'
    MOUNT="$TMP/disk"
    mkdir -p "$MOUNT"
    hdiutil attach -nobrowse -readonly -quiet -mountpoint "$MOUNT" "$TMP/$name"
    source=$(find "$MOUNT" -maxdepth 1 -name '*.app' -print | head -n 1)
    [ -n "$source" ] || die "r2modmac's download has no app in it."

    folder='/Applications'
    if [ ! -w "$folder" ]; then folder="$HOME/Applications"; mkdir -p "$folder"; fi
    target="$folder/r2modmac.app"
    rm -rf "$target"
    ditto "$source" "$target"
    hdiutil detach -quiet "$MOUNT" || true
    MOUNT=''

    # r2modmac isn't signed by Apple. macOS only blocks it ("damaged") when it carries the
    # downloaded-from-the-internet mark; curl doesn't add one, but clear it to be sure.
    xattr -cr "$target" 2>/dev/null || true
    APP="$target"
}

# ---------- Main ----------

say ''
say "${CYAN}SoapieScripts mods for On-Together${PLAIN}"
say ''

APP=''
if APP=$(find_app); then
    say 'r2modmac is already installed.'
elif [ "$INSTALL_APP" = '0' ]; then
    APP=''
    say 'Skipping r2modmac, as asked.'
else
    APP=''
    install_app
    good "r2modmac is installed in ${APP%/r2modmac.app}."
fi

say ''
say 'Getting the mod list from Thunderstore...'
fetch "https://thunderstore.io/c/$COMMUNITY/api/v1/package/" "$TMP/packages.json"
PLAN=$(helper mods "$TMP/packages.json" "$TEAM" "$LOADER" "$SKIP")

mkdir -p "$TMP/profile" "$OUT_DIR"
OUT_DIR=$(cd "$OUT_DIR" && pwd)
OUT="$OUT_DIR/$PROFILE.r2z"
R2X="$TMP/profile/export.r2x"

# The same layout r2modman uses when it exports a profile to a file.
printf 'profileName: %s\nmods:\n' "$PROFILE" > "$R2X"
say ''
say "Mods in the profile file:"
while IFS=$'\t' read -r kind first second third; do
    case "$kind" in
        mod)
            IFS=. read -r major minor patch <<< "$second"
            printf '  - name: %s\n    version:\n      major: %s\n      minor: %s\n      patch: %s\n    enabled: true\n' \
                "$first" "${major:-0}" "${minor:-0}" "${patch:-0}" >> "$R2X"
            say "  $third $second"
            ;;
        skip) say "  Leaving out $first, as asked." ;;
        warn) warn "  $first" ;;
    esac
done <<< "$PLAN"

rm -f "$OUT"
(cd "$TMP/profile" && zip -q -X "$OUT" export.r2x)
good "Saved $OUT"

say ''
good 'All done! Now, in r2modmac:'
say '  1. Pick On-Together.'
say '  2. Click Import Profile, then "Select .r2z or .zip file", and pick'
say "     $PROFILE.r2z (it's in $(basename "$OUT_DIR"))."
say '  3. Choose MacOS and click Import.'
say '  4. Click Apply to Game, then Launch Modded.'
say ''
say "Later: the profile's Updates tab in r2modmac updates the mods. To add new"
say "mods, run this again and drag the new $PROFILE.r2z onto the open profile."

if [ "$OPEN_AT_END" = '1' ]; then
    open -R "$OUT" >/dev/null 2>&1 || true
    if [ -n "$APP" ]; then open "$APP" >/dev/null 2>&1 || true; fi
fi
