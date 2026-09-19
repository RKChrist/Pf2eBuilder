#!/usr/bin/env bash
# Audits the AoN import against the licensing policy. It re-parses FieldPolicy.cs and checks the
# snapshot, out/seed, out/unmapped and out/oversize against what it finds there. The exit code is the
# number of failed checks AND input guards, so a missing snapshot counts the same as a failed check.
set -u

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$SCRIPT_DIR/../.." && pwd)
POLICY_CS="$ROOT/tools/rules-import/FieldPolicy.cs"
SEED_DIR="$ROOT/tools/rules-import/out/seed"
UNMAPPED_DIR="$ROOT/tools/rules-import/out/unmapped"
OVERSIZE_DIR="$ROOT/tools/rules-import/out/oversize"
EXCLUDED_DIR="$ROOT/tools/rules-import/out/excluded"
SNAPSHOT_ROOT="$ROOT/Sources/aon-snapshot"

TMP=$(mktemp -d) || exit 99
trap 'rm -rf "$TMP"' EXIT

FAILURES=0

heading() { printf '\n=== %s ===\n' "$1"; }
pass() { printf 'PASS  %s\n' "$1"; }
fail() { printf 'FAIL  %s\n' "$1"; FAILURES=$((FAILURES + 1)); }
info() { printf 'INFO  %s\n' "$1"; }

# node here is the Windows binary even under Git Bash, so it resolves /c/... against the current
# drive and silently reads nothing. Every path handed to node goes through this first.
winpath() {
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$1"
    return
  fi
  wp_dir=$(dirname "$1")
  wp_base=$(basename "$1")
  if [ -d "$wp_dir" ] && wp_native=$(cd "$wp_dir" && pwd -W 2>/dev/null) && [ -n "$wp_native" ]; then
    printf '%s/%s\n' "$wp_native" "$wp_base"
  else
    printf '%s\n' "$1"
  fi
}

EXPECTED_COUNTS='equipment 9089
feat 8832
action 4218
spell 2762
class-feature 1366
trait 921
deity 718
background 642
weapon 614
heritage 438
archetype 347
ritual 232
language 155
condition 98
ancestry 94
armor 75
class 51
skill 50
lesson 31
bloodline 28
arcane-school 27
patron 27
eidolon 26
mystery 22
ikon 21
implement 19
epithet 18
instinct 16
hybrid-study 15
apparition 14
cause 13
druidic-order 13
conscious-mind 12
style 11
way 11
arcane-thesis 10
racket 10
methodology 9
muse 9
research-field 8
subconscious-mind 8
hunters-edge 7
innovation 7
element 6
doctrine 5
tradition 5
grim-fascination 4
practice 4
fatal-method 2
shield 32
item-bonus 1369
source 254
domain 124
familiar-ability 191
familiar-specific 47
animal-companion 115
animal-companion-specialization 17
animal-companion-advanced 8
animal-companion-unique 2
follower 6
weapon-group 17
armor-group 7
runesmith-rune 44
draconic-exemplar 44
tactic 37
class-kit 32
relic 219
set-relic 14
curse 92
deity-category 40
mythic-calling 15
skill-general-action 25
hellknight-order 14
deviant-ability-classification 10'

SNAPSHOT_DIR=""
for candidate in "$SNAPSHOT_ROOT"/*/; do
  if [ -f "${candidate}manifest.json" ]; then
    SNAPSHOT_DIR="${candidate%/}"
  fi
done

heading "Inputs"
if command -v node >/dev/null 2>&1; then
  info "node $(node --version)"
else
  fail "node is not on PATH; every JSON-parsing check below is unrunnable"
fi
if [ -f "$POLICY_CS" ]; then info "policy   $POLICY_CS"; else fail "missing $POLICY_CS"; fi
if [ -d "$SEED_DIR" ]; then info "seed     $SEED_DIR"; else fail "missing $SEED_DIR"; fi
if [ -d "$UNMAPPED_DIR" ]; then info "unmapped $UNMAPPED_DIR"; else fail "missing $UNMAPPED_DIR"; fi
if [ -d "$OVERSIZE_DIR" ]; then info "oversize $OVERSIZE_DIR"; else fail "missing $OVERSIZE_DIR"; fi
if [ -n "$SNAPSHOT_DIR" ]; then
  info "snapshot $SNAPSHOT_DIR"
else
  fail "no snapshot index directory with a manifest.json under $SNAPSHOT_ROOT"
fi

cat > "$TMP/policy.js" <<'JS'
const fs = require('fs');
const src = fs.readFileSync(process.argv[2], 'utf8').replace(/\r/g, '');
const outDir = process.argv[3];

function literals(anchor, open, close) {
  const a = src.indexOf(anchor);
  if (a < 0) return null;
  const s = src.indexOf(open, a + anchor.length);
  if (s < 0) return null;
  const e = src.indexOf(close, s);
  if (e < 0) return null;
  return (src.slice(s + 1, e).match(/"[^"]*"/g) || []).map(t => t.slice(1, -1));
}

const groups = [
  ['wire', 'public static readonly IReadOnlyList<string> WireExcludes =', '[', '];'],
  ['prose', 'public static readonly IReadOnlyList<string> ProseFieldOrder =', '[', '];'],
  ['allow', 'public static readonly FrozenSet<string> SeedAllowList = new[]', '{', '}.ToFrozenSet'],
  ['ceiling', 'public static readonly FrozenSet<string> CeilingFields = new[]', '{', '}.ToFrozenSet'],
];

for (const [name, anchor, open, close] of groups) {
  const found = literals(anchor, open, close);
  if (found === null) {
    console.log(name + ' ANCHOR-NOT-FOUND');
    continue;
  }
  fs.writeFileSync(outDir + '/' + name + '.txt', found.map(n => n + '\n').join(''));
  console.log(name + ' ' + found.length);
}

const max = src.match(/public const int LabelCeiling = (\d+);/);
console.log('ceilingmax ' + (max === null ? 'CONSTANT-NOT-FOUND' : max[1]));
JS

heading "Field policy parsed from FieldPolicy.cs"
POLICY_OUT=""
if [ -f "$POLICY_CS" ] && command -v node >/dev/null 2>&1; then
  POLICY_OUT=$(node "$(winpath "$TMP/policy.js")" "$(winpath "$POLICY_CS")" "$(winpath "$TMP")" 2>&1)
fi
policy_count() {
  printf '%s\n' "$POLICY_OUT" | awk -v k="$1" '$1 == k && $2 ~ /^[0-9]+$/ { print $2 }'
}
WIRE_N=$(policy_count wire)
PROSE_N=$(policy_count prose)
ALLOW_N=$(policy_count allow)
CEILING_N=$(policy_count ceiling)
CEILING_MAX=$(policy_count ceilingmax)
POLICY_OK=1
for group in "WireExcludes:$WIRE_N" "ProseFieldOrder:$PROSE_N" "SeedAllowList:$ALLOW_N" \
  "CeilingFields:$CEILING_N"; do
  name=${group%%:*}
  n=${group#*:}
  if [ -n "$n" ] && [ "$n" -gt 0 ] 2>/dev/null; then
    info "$name parsed $n names"
  else
    fail "$name did not parse out of FieldPolicy.cs; dependent checks cannot run"
    POLICY_OK=0
  fi
done
if [ -n "$CEILING_MAX" ] && [ "$CEILING_MAX" -gt 0 ] 2>/dev/null; then
  info "LabelCeiling parsed as $CEILING_MAX characters"
else
  fail "LabelCeiling did not parse out of FieldPolicy.cs; dependent checks cannot run"
  POLICY_OK=0
fi
if [ "$POLICY_OK" -eq 0 ]; then
  printf '%s\n' "$POLICY_OUT" | sed 's/^/      /'
fi

PROSE_ALT=""
if [ "$POLICY_OK" -eq 1 ]; then
  PROSE_ALT=$(tr '\n' '|' < "$TMP/prose.txt" | sed 's/|$//')
fi
PROSE_PATTERN=""
[ -n "$PROSE_ALT" ] && PROSE_PATTERN="\"($PROSE_ALT)\"[[:space:]]*:"
MARKDOWN_PATTERN='"[A-Za-z0-9_]+_markdown"[[:space:]]*:'

seed_files_present() {
  [ -d "$SEED_DIR" ] && [ -n "$(find -L "$SEED_DIR" -name '*.json' -type f 2>/dev/null | head -n 1)" ]
}

# A search that errors, or a pattern built from an empty list, reports FAIL and never PASS. A
# licensing verifier that silently checks nothing is worse than one that is loudly broken.
run_search() {
  rs_label=$1
  rs_pattern=$2
  rs_mode=$3
  if [ -z "$rs_pattern" ]; then
    fail "$rs_label could not run: empty search pattern"
    return
  fi
  if ! seed_files_present; then
    fail "$rs_label could not run: no *.json under $SEED_DIR"
    return
  fi
  rs_out=$(grep -rn "$rs_mode" -- "$rs_pattern" "$SEED_DIR" 2>&1)
  rs_rc=$?
  if [ "$rs_rc" -eq 1 ]; then
    pass "$rs_label: no match"
  elif [ "$rs_rc" -eq 0 ]; then
    fail "$rs_label: matched"
    printf '%s\n' "$rs_out" | head -n 20 | sed 's/^/      /'
  else
    fail "$rs_label could not run: grep exited $rs_rc"
    printf '%s\n' "$rs_out" | head -n 5 | sed 's/^/      /'
  fi
}

cat > "$TMP/manifest.js" <<'JS'
const fs = require('fs');
const m = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const cats = m.categories || {};
for (const [name, c] of Object.entries(cats)) {
  console.log(['CAT', name, c.indexed, c.written, c.droppedAsLegacy, c.legacyTargetMissing].join(' '));
}
console.log('COUNT ' + Object.keys(cats).length);
JS

heading "1. Manifest reconciliation against published AoN counts"
MANIFEST_JSON=""
[ -n "$SNAPSHOT_DIR" ] && MANIFEST_JSON="$SNAPSHOT_DIR/manifest.json"
MANIFEST_OUT=""
if [ -n "$MANIFEST_JSON" ] && [ -f "$MANIFEST_JSON" ] && command -v node >/dev/null 2>&1; then
  MANIFEST_OUT=$(node "$(winpath "$TMP/manifest.js")" "$(winpath "$MANIFEST_JSON")" 2>&1)
fi
MANIFEST_ROWS=$(printf '%s\n' "$MANIFEST_OUT" | awk '$1 == "CAT"' | wc -l | tr -d ' ')
if [ "$MANIFEST_ROWS" -eq 0 ]; then
  fail "check 1 could not run: no categories read from ${MANIFEST_JSON:-<no manifest>}"
  printf '%s\n' "$MANIFEST_OUT" | head -n 5 | sed 's/^/      /'
else
  printf '%-14s %9s %9s %9s %9s %9s  %s\n' category indexed written dropped dangling expected status
  bad_rows=0
  while read -r category expected; do
    row=$(printf '%s\n' "$MANIFEST_OUT" | awk -v c="$category" '$1 == "CAT" && $2 == c { print; exit }')
    if [ -z "$row" ]; then
      printf '%-14s %9s %9s %9s %9s %9s  %s\n' "$category" - - - - "$expected" ABSENT
      bad_rows=$((bad_rows + 1))
      continue
    fi
    read -r _tag _cat indexed written dropped dangling <<EOF
$row
EOF
    status=ok
    for n in "$indexed" "$written" "$dropped" "$dangling"; do
      case $n in
        ''|*[!0-9]*) status=malformed ;;
      esac
    done
    if [ "$status" = ok ] && [ "$indexed" != "$expected" ]; then
      status="indexed!=$expected"
    fi
    if [ "$status" = ok ] && [ "$((written + dropped))" -ne "$indexed" ]; then
      status="written+dropped!=indexed"
    fi
    [ "$status" = ok ] || bad_rows=$((bad_rows + 1))
    printf '%-14s %9s %9s %9s %9s %9s  %s\n' \
      "$category" "$indexed" "$written" "$dropped" "$dangling" "$expected" "$status"
  done <<EOF
$EXPECTED_COUNTS
EOF
  extra=$(printf '%s\n' "$MANIFEST_OUT" | awk '$1 == "CAT" { print $2 }' | while read -r c; do
    printf '%s\n' "$EXPECTED_COUNTS" | awk -v c="$c" '$1 == c { found = 1 } END { if (!found) print c }'
  done)
  if [ -n "$extra" ]; then
    printf '      category not in the published table: %s\n' "$(printf '%s' "$extra" | tr '\n' ' ')"
    bad_rows=$((bad_rows + 1))
  fi
  if [ "$bad_rows" -eq 0 ]; then
    pass "all 18 categories reconcile with the published counts"
  else
    fail "$bad_rows of 18 category rows do not reconcile"
  fi
fi

heading "2. Excluded field names as JSON keys in out/seed"
if [ -n "$PROSE_ALT" ]; then
  printf '      grep -rEn -- %s %s\n' "'$PROSE_PATTERN'" "$SEED_DIR"
fi
run_search "excluded field names in seed" "$PROSE_PATTERN" -E
printf '      grep -rEn -- %s %s\n' "'$MARKDOWN_PATTERN'" "$SEED_DIR"
run_search "_markdown keys in seed" "$MARKDOWN_PATTERN" -E

heading "3. Rule prose in out/seed"
for phrase in 'struggle to control your nerves' 'always includes a value' 'Source Player Core'; do
  run_search "prose phrase \"$phrase\"" "$phrase" -F
done

cat > "$TMP/seedscan.js" <<'JS'
const fs = require('fs');
const path = require('path');
const dir = process.argv[2];
const lines = f => fs.readFileSync(f, 'utf8').split('\n').map(s => s.trim()).filter(Boolean);
const allow = new Set(lines(process.argv[3]));
const prose = new Set(lines(process.argv[4]));
const ceilingFields = new Set(lines(process.argv[5]));
const ceiling = Number(process.argv[6]);
for (const k of ['id', 'name', 'category', 'sourceUrl']) allow.add(k);

const PREFIX = 'https://2e.aonprd.com/';
const ASPX = /^[A-Za-z0-9._%-]+\.aspx(\?[^#]*)?$/;

let records = 0, urlChecked = 0, urlMalformed = 0, urlIdDerived = 0, oversize = 0;
const keys = new Set(), bad = new Set(), leak = new Set(), pages = new Map();
const malformedSamples = [], oversizeSamples = [];

for (const file of fs.readdirSync(dir).filter(f => f.endsWith('.json')).sort()) {
  const arr = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));
  for (const r of arr) {
    records++;
    for (const [k, v] of Object.entries(r)) {
      keys.add(k);
      if (!allow.has(k)) {
        bad.add(k);
        if (prose.has(k) || k.endsWith('_markdown')) leak.add(k);
      }
      if (ceilingFields.has(k)) {
        const len = typeof v === 'string' ? v.length : JSON.stringify(v).length;
        if (len > ceiling) {
          oversize++;
          if (oversizeSamples.length < 50) oversizeSamples.push(r.id + ' ' + k + ' ' + len + ' ' + file);
        }
      }
    }
    if (r.name === 'Frightened') console.log('FRIGHTENED ' + file + ' ' + r.id);
    if (r.id === 'condition-19') console.log('ID19 ' + file);
    if (typeof r.sourceUrl === 'string') {
      urlChecked++;
      const rest = r.sourceUrl.startsWith(PREFIX) ? r.sourceUrl.slice(PREFIX.length) : null;
      if (rest === null || !ASPX.test(rest)) {
        urlMalformed++;
        if (malformedSamples.length < 10) malformedSamples.push(r.id + ' ' + r.sourceUrl);
      } else {
        const page = rest.split('?')[0];
        pages.set(page, (pages.get(page) || 0) + 1);
      }
      const segments = (rest === null ? '' : rest).split('?')[0].split('/');
      if (typeof r.id === 'string' && segments.includes(r.id)) urlIdDerived++;
    }
  }
  console.log('FILE ' + file + ' ' + arr.length);
}

console.log('RECORDS ' + records);
console.log('DISTINCTKEYS ' + keys.size);
for (const k of [...bad].sort()) console.log('BADKEY ' + k);
for (const k of [...leak].sort()) console.log('LEAKKEY ' + k);
console.log('URLCHECKED ' + urlChecked);
console.log('URLMALFORMED ' + urlMalformed);
console.log('URLIDDERIVED ' + urlIdDerived);
console.log('OVERSIZE ' + oversize);
for (const s of malformedSamples) console.log('URLBAD ' + s);
for (const s of oversizeSamples) console.log('TOOLONG ' + s);
for (const [p, n] of [...pages].sort()) console.log('PAGE ' + p + ' ' + n);
JS

SEEDSCAN_OUT=""
if seed_files_present && [ "$POLICY_OK" -eq 1 ] && command -v node >/dev/null 2>&1; then
  SEEDSCAN_OUT=$(node "$(winpath "$TMP/seedscan.js")" "$(winpath "$SEED_DIR")" \
    "$(winpath "$TMP/allow.txt")" "$(winpath "$TMP/prose.txt")" \
    "$(winpath "$TMP/ceiling.txt")" "$CEILING_MAX" 2>&1)
fi
seedscan_value() { printf '%s\n' "$SEEDSCAN_OUT" | awk -v k="$1" '$1 == k { print $2; exit }'; }
SEED_RECORDS=$(seedscan_value RECORDS)
[ -n "$SEED_RECORDS" ] || SEED_RECORDS=0

heading "4. Allow-list conformance of every seed key"
if [ "$SEED_RECORDS" -eq 0 ]; then
  fail "check 4 could not run: zero seed records parsed from $SEED_DIR"
  printf '%s\n' "$SEEDSCAN_OUT" | head -n 5 | sed 's/^/      /'
else
  info "$SEED_RECORDS records, $(seedscan_value DISTINCTKEYS) distinct keys, allow-list $ALLOW_N names plus id/name/category/sourceUrl"
  printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "FILE" { printf "      %s %s records\n", $2, $3 }'
  bad_keys=$(printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "BADKEY" { print $2 }')
  leak_keys=$(printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "LEAKKEY" { print $2 }')
  if [ -n "$leak_keys" ]; then
    fail "prose leak: seed keys that are excluded prose fields: $(printf '%s' "$leak_keys" | tr '\n' ' ')"
  fi
  if [ -n "$bad_keys" ]; then
    fail "keys outside the allow-list: $(printf '%s' "$bad_keys" | tr '\n' ' ')"
  else
    pass "every seed key is id/name/category/sourceUrl or on SeedAllowList"
  fi
fi

cat > "$TMP/snapscan.js" <<'JS'
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const dir = process.argv[2];
const names = fs.readFileSync(process.argv[3], 'utf8').split('\n').map(s => s.trim()).filter(Boolean);
if (names.length === 0) {
  console.log('FILES 0');
  console.log('BYTES 0');
  process.exit(0);
}
const nameRe = new RegExp('"(' + names.join('|') + ')"\\s*:', 'g');
const mdRe = /"[A-Za-z0-9_]+_markdown"\s*:/g;

let files = 0, bytes = 0;
const nameHits = new Map(), mdHits = new Map();
for (const f of fs.readdirSync(dir).filter(n => n.endsWith('.ndjson.gz')).sort()) {
  const text = zlib.gunzipSync(fs.readFileSync(path.join(dir, f))).toString('utf8');
  files++;
  bytes += text.length;
  const records = text.split('\n').filter(Boolean).length;
  console.log('FILE ' + f + ' ' + records + ' ' + text.length);
  for (const m of text.matchAll(nameRe)) {
    const k = f + ' ' + m[1];
    nameHits.set(k, (nameHits.get(k) || 0) + 1);
  }
  for (const m of text.matchAll(mdRe)) {
    const k = f + ' ' + m[0];
    mdHits.set(k, (mdHits.get(k) || 0) + 1);
  }
}
console.log('FILES ' + files);
console.log('BYTES ' + bytes);
for (const [k, n] of [...nameHits].sort()) console.log('NAMEHIT ' + k + ' ' + n);
for (const [k, n] of [...mdHits].sort()) console.log('MDHIT ' + k + ' ' + n);
JS

heading "5. Excluded field names and _markdown keys in the decompressed snapshot"
SNAPSCAN_OUT=""
if [ -n "$SNAPSHOT_DIR" ] && [ "$POLICY_OK" -eq 1 ] && command -v node >/dev/null 2>&1; then
  SNAPSCAN_OUT=$(node "$(winpath "$TMP/snapscan.js")" "$(winpath "$SNAPSHOT_DIR")" \
    "$(winpath "$TMP/prose.txt")" 2>&1)
fi
SNAP_FILES=$(printf '%s\n' "$SNAPSCAN_OUT" | awk '$1 == "FILES" { print $2; exit }')
SNAP_BYTES=$(printf '%s\n' "$SNAPSCAN_OUT" | awk '$1 == "BYTES" { print $2; exit }')
[ -n "$SNAP_FILES" ] || SNAP_FILES=0
[ -n "$SNAP_BYTES" ] || SNAP_BYTES=0
if [ "$SNAP_FILES" -eq 0 ] || [ "$SNAP_BYTES" -eq 0 ]; then
  fail "check 5 could not run: decompressed $SNAP_FILES files and $SNAP_BYTES bytes under ${SNAPSHOT_DIR:-<no snapshot>}"
  printf '%s\n' "$SNAPSCAN_OUT" | head -n 5 | sed 's/^/      /'
else
  info "$SNAP_FILES gz files, $SNAP_BYTES bytes decompressed, searched for $PROSE_N excluded names"
  printf '%s\n' "$SNAPSCAN_OUT" | awk '$1 == "FILE" { printf "      %s %s records %s bytes\n", $2, $3, $4 }'
  snap_name_hits=$(printf '%s\n' "$SNAPSCAN_OUT" | awk '$1 == "NAMEHIT"')
  snap_md_hits=$(printf '%s\n' "$SNAPSCAN_OUT" | awk '$1 == "MDHIT"')
  if [ -n "$snap_name_hits" ]; then
    fail "excluded field names present on the wire"
    printf '%s\n' "$snap_name_hits" | head -n 20 | sed 's/^/      /'
  else
    pass "no excluded field name appears as a key in the snapshot"
  fi
  if [ -n "$snap_md_hits" ]; then
    fail "_markdown keys present on the wire"
    printf '%s\n' "$snap_md_hits" | head -n 20 | sed 's/^/      /'
  else
    pass "no _markdown key appears in the snapshot"
  fi
fi

heading "6. Remaster filter on out/seed/condition.json"
if [ ! -f "$SEED_DIR/condition.json" ]; then
  fail "check 6 could not run: missing $SEED_DIR/condition.json"
elif [ "$SEED_RECORDS" -eq 0 ]; then
  fail "check 6 could not run: zero seed records parsed"
else
  frightened=$(printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "FRIGHTENED" && $2 == "condition.json" { print $3 }')
  frightened_n=$(printf '%s' "$frightened" | grep -c . )
  id19=$(printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "ID19"' | wc -l | tr -d ' ')
  info "Frightened records: $frightened_n [$(printf '%s' "$frightened" | tr '\n' ' ')], condition-19 records: $id19"
  if [ "$frightened_n" -eq 1 ] && [ "$frightened" = "condition-76" ] && [ "$id19" -eq 0 ]; then
    pass "exactly one Frightened (condition-76) and no condition-19"
  else
    fail "expected exactly one Frightened with id condition-76 and no condition-19"
  fi
fi

heading "7. sourceUrl provenance"
if [ "$SEED_RECORDS" -eq 0 ]; then
  fail "check 7 could not run: zero seed records parsed"
else
  url_checked=$(seedscan_value URLCHECKED)
  url_malformed=$(seedscan_value URLMALFORMED)
  url_idderived=$(seedscan_value URLIDDERIVED)
  pages=$(printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "PAGE"')
  pages_n=$(printf '%s' "$pages" | grep -c . )
  info "records with a sourceUrl: $url_checked of $SEED_RECORDS, malformed: $url_malformed, id-derived: $url_idderived"
  printf '%s\n' "$pages" | awk 'NF { printf "      %s  %s records\n", $2, $3 }'
  if [ "$url_checked" -ne "$SEED_RECORDS" ]; then
    fail "$((SEED_RECORDS - url_checked)) records carry no sourceUrl"
  fi
  if [ "$url_malformed" -ne 0 ]; then
    fail "$url_malformed sourceUrl values are not https://2e.aonprd.com/<page>.aspx"
    printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "URLBAD"' | head -n 10 | sed 's/^/      /'
  fi
  if [ "$url_idderived" -ne 0 ]; then
    fail "$url_idderived sourceUrl values embed the record id as a path segment"
  fi
  if [ "$url_checked" -eq "$SEED_RECORDS" ] && [ "$url_malformed" -eq 0 ] && [ "$url_idderived" -eq 0 ]; then
    pass "all $url_checked sourceUrl values are real AoN .aspx pages across $pages_n distinct pages"
  fi
fi

heading "8. Length ceiling on the re-included label fields in out/seed"
if [ "$SEED_RECORDS" -eq 0 ]; then
  fail "check 8 could not run: zero seed records parsed"
else
  oversize_n=$(seedscan_value OVERSIZE)
  if [ -z "$oversize_n" ]; then
    fail "check 8 could not run: seedscan reported no ceiling result"
    printf '%s\n' "$SEEDSCAN_OUT" | head -n 5 | sed 's/^/      /'
  elif [ "$oversize_n" -eq 0 ]; then
    pass "no seeded value of the $CEILING_N ceiling fields exceeds $CEILING_MAX characters"
  else
    fail "$oversize_n seeded values exceed the $CEILING_MAX-character ceiling"
    printf '%s\n' "$SEEDSCAN_OUT" | awk '$1 == "TOOLONG"' | head -n 20 | sed 's/^/      /'
  fi
fi

cat > "$TMP/unmapped.js" <<'JS'
const fs = require('fs');
const path = require('path');
const dir = process.argv[2];
const oversizeDir = process.argv[3];
const jsonFiles = d => fs.existsSync(d) ? fs.readdirSync(d).filter(f => f.endsWith('.json')).sort() : [];

const oversize = new Map();
for (const file of jsonFiles(oversizeDir)) {
  oversize.set(file, JSON.parse(fs.readFileSync(path.join(oversizeDir, file), 'utf8')).length);
}

let total = 0, oversizeTotal = 0;
for (const file of jsonFiles(dir)) {
  const arr = JSON.parse(fs.readFileSync(path.join(dir, file), 'utf8'));
  const groups = new Map();
  for (const r of arr) {
    const reason = typeof r.reason === 'string' ? r.reason : '';
    const head = (reason.split(':')[0] || '(no reason)').trim() || '(no reason)';
    groups.set(head, (groups.get(head) || 0) + 1);
  }
  total += arr.length;
  const over = oversize.get(file) || 0;
  oversizeTotal += over;
  console.log('CAT ' + path.basename(file, '.json') + ' ' + arr.length + ' ' + over);
  for (const [g, n] of [...groups].sort((a, b) => b[1] - a[1])) console.log('REASON ' + n + ' ' + g);
}
console.log('TOTAL ' + total + ' ' + oversizeTotal);
JS

heading "9. Unmapped and oversize records and snapshot size (informational)"
if [ -d "$UNMAPPED_DIR" ] && command -v node >/dev/null 2>&1; then
  UNMAPPED_OUT=$(node "$(winpath "$TMP/unmapped.js")" "$(winpath "$UNMAPPED_DIR")" \
    "$(winpath "$OVERSIZE_DIR")" 2>&1)
  printf '%s\n' "$UNMAPPED_OUT" | awk '
    $1 == "CAT" { printf "      %-14s %6s unmapped %6s oversize\n", $2, $3, $4; next }
    $1 == "REASON" { $1 = ""; $2 = ""; printf "        %s\n", $0; next }
    $1 == "TOTAL" { printf "      total unmapped: %s, total oversize: %s\n", $2, $3; next }
    { printf "      %s\n", $0 }'
else
  info "unmapped and oversize counts unavailable"
fi
if [ -n "$SNAPSHOT_DIR" ]; then
  snap_total=0
  for f in "$SNAPSHOT_DIR"/*; do
    [ -f "$f" ] || continue
    size=$(wc -c < "$f" | tr -d ' ')
    snap_total=$((snap_total + size))
    printf '      %-24s %10s bytes\n' "$(basename "$f")" "$size"
  done
  printf '      snapshot on disk: %s bytes\n' "$snap_total"
fi

cat > "$TMP/sitehidden.js" <<'JS'
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');
const [snapshotDir, seedDir, excludedDir] = process.argv.slice(2);
const flagged = new Map();
for (const f of fs.readdirSync(snapshotDir).filter(n => n.endsWith('.ndjson.gz')).sort()) {
  const category = f.replace('.ndjson.gz', '');
  const ids = new Set();
  for (const line of zlib.gunzipSync(fs.readFileSync(path.join(snapshotDir, f))).toString('utf8').split('\n')) {
    if (!line) continue;
    const r = JSON.parse(line);
    if (r.exclude_from_search === true) ids.add(r.id);
  }
  flagged.set(category, ids);
}
let survivors = 0, total = 0;
for (const [category, ids] of flagged) {
  total += ids.size;
  const seedFile = path.join(seedDir, category + '.json');
  const seeded = fs.existsSync(seedFile) ? JSON.parse(fs.readFileSync(seedFile, 'utf8')) : [];
  for (const r of seeded) {
    if (ids.has(r.id)) {
      survivors++;
      if (survivors <= 20) console.log('SURVIVOR ' + category + ' ' + r.id + ' ' + r.name);
    }
  }
  const excludedFile = path.join(excludedDir, category + '.json');
  const recorded = fs.existsSync(excludedFile) ? JSON.parse(fs.readFileSync(excludedFile, 'utf8')).length : -1;
  if (ids.size > 0 || recorded > 0) console.log('CAT ' + category + ' ' + ids.size + ' ' + recorded);
}
console.log('TOTAL ' + total + ' ' + survivors);
JS

heading "10. Records Archives of Nethys excludes from its own search"
if [ -z "$SNAPSHOT_DIR" ] || [ "$SEED_RECORDS" -eq 0 ] || ! command -v node >/dev/null 2>&1; then
  fail "check 10 could not run: it needs the snapshot, a parsed seed and node"
else
  HIDDEN_OUT=$(node "$(winpath "$TMP/sitehidden.js")" "$(winpath "$SNAPSHOT_DIR")" "$(winpath "$SEED_DIR")" \
    "$(winpath "$EXCLUDED_DIR")" 2>&1)
  hidden_total=$(printf '%s\n' "$HIDDEN_OUT" | awk '$1 == "TOTAL" { print $2 }')
  hidden_left=$(printf '%s\n' "$HIDDEN_OUT" | awk '$1 == "TOTAL" { print $3 }')
  printf '%s\n' "$HIDDEN_OUT" | awk '$1 == "CAT" { printf "      %-18s %6s flagged %6s recorded in out/excluded\n", $2, $3, $4 }'
  unrecorded=$(printf '%s\n' "$HIDDEN_OUT" | awk '$1 == "CAT" && $3 != $4 { print $2 }')
  if [ -z "$hidden_total" ] || [ "$hidden_total" -eq 0 ]; then
    fail "check 10 found no flagged record in the snapshot, so it checked nothing"
    printf '%s\n' "$HIDDEN_OUT" | head -n 5 | sed 's/^/      /'
  elif [ "$hidden_left" -ne 0 ]; then
    fail "$hidden_left of $hidden_total flagged records reached the seed"
    printf '%s\n' "$HIDDEN_OUT" | awk '$1 == "SURVIVOR"' | sed 's/^/      /'
  elif [ -n "$unrecorded" ]; then
    fail "out/excluded does not record every flagged record: $(printf '%s' "$unrecorded" | tr '\n' ' ')"
  else
    pass "none of the $hidden_total flagged records is seeded, and out/excluded records each one"
  fi
fi

heading "Summary"
printf '%d check(s) FAILED\n' "$FAILURES"
exit "$FAILURES"
