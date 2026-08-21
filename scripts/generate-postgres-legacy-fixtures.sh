#!/usr/bin/env bash
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
inventory="$repo/src/server/tests/Collectify.PostgresTests/Fixtures/legacy-state-inventory.json"
generated="$repo/src/server/tests/Collectify.PostgresTests/Fixtures/generated"
image='postgres:17-alpine@sha256:d4bb0a8c1b7bb2e29f976d099e7bfb9a5d8858cffe9e46b35cd302cd1f1f8168'
approved_base='86500ebd2648793e4be1a80c028bcbffcc6b51ce'

mode=${1:-}
case "$mode" in
  --enumerate-only) test $# -eq 1 ;;
  --base) test $# -eq 2; test "$2" = "$approved_base" ;;
  *) echo 'usage: generate-postgres-legacy-fixtures.sh --enumerate-only | --base 86500ebd2648793e4be1a80c028bcbffcc6b51ce' >&2; exit 2 ;;
esac

test -f "$inventory"

# This classifier intentionally shares no implementation with the C# oracle.
enumerate() {
  python3 - "$repo" "$inventory" <<'PY'
import hashlib, json, pathlib, re, subprocess, sys, xml.etree.ElementTree as ET
repo, inventory_path = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
data = json.loads(inventory_path.read_text(encoding='utf-8'))
def git(*args, binary=False):
    value = subprocess.check_output(['git', '-C', str(repo), *args])
    return value if binary else value.decode()
commits = git('log', '--first-parent', '--reverse', '--format=%H',
              data['firstPostgresCommit'] + '^..' + data['baseCommit']).splitlines()
if len(commits) != 38:
    raise SystemExit(f'expected 38 first-parent states, got {len(commits)}')
selected, previous = [], None
for commit in commits:
    material = b''.join(git('ls-tree', '-r', commit, '--', value, binary=True)
                        for value in data['modelInputs'])
    signature = hashlib.sha256(material).hexdigest()
    root = ET.fromstring(git('show', f"{commit}:{data['packageInput']}"))
    versions = {node.attrib['Include']: node.attrib['Version'] for node in root.iter('PackageVersion')}
    state = {'commit': commit,
             'efCoreVersion': versions['Microsoft.EntityFrameworkCore.Sqlite'],
             'npgsqlVersion': versions['Npgsql.EntityFrameworkCore.PostgreSQL'],
             'modelInputSignature': signature}
    if previous is None or any(state[k] != previous[k]
                               for k in ('efCoreVersion','npgsqlVersion','modelInputSignature')):
        selected.append(state)
    previous = state
expected = data['states']
if selected != expected:
    raise SystemExit('independent transition enumeration differs from immutable inventory')
if len(expected) != 12 or len({x['commit'] for x in expected}) != 12:
    raise SystemExit('inventory must contain exactly 12 unique states')
if any(not re.fullmatch(r'[0-9a-f]{40}', x['commit']) for x in expected):
    raise SystemExit('inventory contains a non-canonical commit identity')
print('\n'.join(x['commit'] for x in selected))
PY
}

if test "$mode" = --enumerate-only; then
  enumerate
  exit 0
fi

test "$(git -C "$repo" branch --show-current)" = feat/postgres-migrations-100
test "$(git -C "$repo" rev-parse HEAD)" = "$approved_base" || {
  # Generation is deliberately run after the clean A2 prerequisite commit, whose
  # ancestry must contain the approved base and whose inputs remain unchanged.
  git -C "$repo" merge-base --is-ancestor "$approved_base" HEAD
}
mapfile -t commits < <(enumerate)
test ${#commits[@]} -eq 12

mapfile -t protected_inputs < <(python3 - "$inventory" <<'PY'
import json, sys
d=json.load(open(sys.argv[1]))
print('\n'.join(d['modelInputs']+[d['packageInput'],
'src/server/Collectify.Infrastructure/DatabaseOptions.cs',
'src/server/Collectify.Infrastructure/Data/CollectifyDbContextExtensions.cs',
'src/server/Collectify.Infrastructure/Data/CollectifyDbContextRegistrationMarker.cs']))
PY
)
test -z "$(git -C "$repo" status --porcelain -- "${protected_inputs[@]}")"

docker_cmd=(docker)
if ! docker info >/dev/null 2>&1; then
  sudo -n docker info >/dev/null
  docker_cmd=(sudo -n docker)
fi
"${docker_cmd[@]}" image inspect "$image" >/dev/null 2>&1 || "${docker_cmd[@]}" pull "$image" >/dev/null

run_root=$(mktemp -d "${generated}.run.XXXXXX")
publish_root="${generated}.new.$$"
active_container=
active_app=
active_worktree=
cleanup_state() {
  if test -n "$active_app"; then kill "$active_app" >/dev/null 2>&1 || true; wait "$active_app" 2>/dev/null || true; active_app=; fi
  if test -n "$active_container"; then "${docker_cmd[@]}" rm -f "$active_container" >/dev/null 2>&1 || true; active_container=; fi
  if test -n "$active_worktree"; then git -C "$repo" worktree remove --force "$active_worktree" >/dev/null 2>&1 || true; active_worktree=; fi
}
cleanup_all() { cleanup_state; rm -rf -- "$run_root" "$publish_root"; }
trap cleanup_all EXIT HUP INT TERM
mkdir -p "$publish_root"

password="C$(openssl rand -hex 24)"
canaries=(CANARY_HOST_ISSUE100 CANARY_USER_ISSUE100 CANARY_PASSWORD_ISSUE100)
redact() { sed -e "s/${password//\//\\/}/[REDACTED]/g" -e 's/CANARY_HOST_ISSUE100/[REDACTED]/g' -e 's/CANARY_USER_ISSUE100/[REDACTED]/g' -e 's/CANARY_PASSWORD_ISSUE100/[REDACTED]/g'; }
test "$(printf '%s\n' "${canaries[*]} $password" | redact)" = '[REDACTED] [REDACTED] [REDACTED] [REDACTED]'
scan_secrets() {
  local roots=("$run_root" "$publish_root")
  test ! -f /home/mforce/.hermes/profiles/kyoder/workspace/collectify-issue100/pr-body.initial.md || roots+=(/home/mforce/.hermes/profiles/kyoder/workspace/collectify-issue100/pr-body.initial.md)
  ! grep -RIlF -- "$password" "${roots[@]}" 2>/dev/null | grep -q .
  local value
  for value in "${canaries[@]}"; do ! grep -RIlF -- "$value" "${roots[@]}" 2>/dev/null | grep -q .; done
}

sdk_version=$(dotnet --version)
docker_version=$("${docker_cmd[@]}" version --format '{{.Server.Version}}')
inventory_sha=$(sha256sum "$inventory" | cut -d' ' -f1)
generator_sha=$(sha256sum "$repo/scripts/generate-postgres-legacy-fixtures.sh" | cut -d' ' -f1)
records="$run_root/records.jsonl"
: > "$records"

for index in "${!commits[@]}"; do
  commit=${commits[$index]}
  state_root="$run_root/$commit"
  active_worktree="$state_root/worktree"
  mkdir -p "$state_root"
  git -C "$repo" worktree add --detach "$active_worktree" "$commit" >/dev/null
  test "$(git -C "$active_worktree" rev-parse HEAD)" = "$commit"
  test -z "$(git -C "$active_worktree" status --porcelain)"
  registration="$active_worktree/src/server/Collectify.Infrastructure/Data/CollectifyDbContextExtensions.cs"
  startup="$active_worktree/src/server/Collectify.Api/Program.cs"
  grep -q 'UseNpgsql' "$registration"
  grep -q 'AddCollectifyDbContext' "$startup"
  grep -q 'EnsurePostgresDatabaseAsync' "$startup"
  grep -q 'EnsureCreatedAsync' "$startup"

  active_container="collectify-issue100-${commit:0:12}-$$"
  "${docker_cmd[@]}" run -d --name "$active_container" -e POSTGRES_USER=collectify_fixture \
    -e POSTGRES_PASSWORD="$password" -e POSTGRES_DB=collectify_fixture -P "$image" >/dev/null
  port=$("${docker_cmd[@]}" port "$active_container" 5432/tcp | awk -F: 'NR==1{print $NF}')
  for attempt in {1..90}; do
    "${docker_cmd[@]}" exec -e PGPASSWORD="$password" "$active_container" pg_isready -U collectify_fixture -d collectify_fixture >/dev/null 2>&1 && break
    test "$attempt" -lt 90 || { echo 'PostgreSQL readiness timed out' >&2; exit 1; }
    sleep 1
  done
  log="$state_root/app.log"
  connection="Host=127.0.0.1;Port=$port;Database=collectify_fixture;Username=collectify_fixture;Password=$password"
  (cd "$active_worktree/src/server" && \
    Collectify__Database__Provider=postgres Collectify__Database__ConnectionString="$connection" \
    ASPNETCORE_URLS=http://127.0.0.1:0 dotnet run --project Collectify.Api --no-launch-profile) >"$log" 2>&1 &
  active_app=$!
  for attempt in {1..300}; do
    kill -0 "$active_app" 2>/dev/null || { redact < "$log" >&2; exit 1; }
    grep -q 'Now listening on:' "$log" && break
    test "$attempt" -lt 300 || { redact < "$log" >&2; echo 'production boot timed out' >&2; exit 1; }
    sleep 1
  done
  raw="$state_root/schema.raw.sql"
  "${docker_cmd[@]}" exec -e PGPASSWORD="$password" "$active_container" pg_dump \
    -U collectify_fixture -d collectify_fixture --schema-only --schema=public --no-owner --no-privileges \
    --no-comments --quote-all-identifiers --restrict-key=COLLECTIFYISSUE100PROVENANCE > "$raw"
  pg_dump_version=$("${docker_cmd[@]}" exec "$active_container" pg_dump --version | tr -d '\r')
  output="$publish_root/$commit"
  mkdir -p "$output"
  python3 - "$raw" "$output/schema.normalized.sql" <<'PY'
import pathlib, re, sys
text=pathlib.Path(sys.argv[1]).read_bytes().decode().replace('\r\n','\n').replace('\r','\n')
lines=[]
for line in text.splitlines():
    if line.startswith('-- Dumped from database version ') or line.startswith('-- Dumped by pg_dump version '): continue
    lines.append(line.rstrip(' \t'))
pathlib.Path(sys.argv[2]).write_text('\n'.join(lines).rstrip('\n')+'\n', encoding='utf-8', newline='\n')
PY
  # Emit catalog rows as tab-separated JSON values, then canonicalize without
  # allowing server or role names into the committed fingerprint.
  catalog_rows="$state_root/catalog.jsonl"
  "${docker_cmd[@]}" exec -i -e PGPASSWORD="$password" "$active_container" psql -qXAt \
    -U collectify_fixture -d collectify_fixture -v ON_ERROR_STOP=1 > "$catalog_rows" <<'SQL'
CREATE TEMP VIEW objects AS SELECT c.oid,c.relname,c.relkind,c.relpersistence,c.relrowsecurity,c.relforcerowsecurity,c.relacl,c.relowner FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public';
SELECT json_build_object('category','databaseSchema','databaseOwnerIsCurrentUser',(SELECT pg_get_userbyid(datdba)=current_user FROM pg_database WHERE datname=current_database()),'schemaOwnerIsDatabaseOwner',(SELECT nspowner=(SELECT datdba FROM pg_database WHERE datname=current_database()) OR pg_get_userbyid(nspowner)='pg_database_owner' FROM pg_namespace WHERE nspname='public'),'currentUserHasUsage',has_schema_privilege(current_user,'public','USAGE'),'currentUserHasCreate',has_schema_privilege(current_user,'public','CREATE'),'publicHasCreate',has_schema_privilege('public','public','CREATE'))::text;
SELECT json_build_object('category','relations','name',relname,'kind',relkind,'persistence',relpersistence,'ownerIsCurrentUser',pg_has_role(current_user,relowner,'MEMBER'),'acl',coalesce(array_to_json(relacl)::json,'[]'::json))::text FROM objects ORDER BY relname;
SELECT json_build_object('category','columns','relation',c.relname,'ordinal',a.attnum,'name',a.attname,'typeOid',a.atttypid,'typeName',format_type(a.atttypid,a.atttypmod),'typmod',a.atttypmod,'length',a.attlen,'notNull',a.attnotnull,'collation',CASE WHEN a.attcollation=0 THEN NULL ELSE co.collname END,'default',pg_get_expr(d.adbin,d.adrelid),'identity',a.attidentity,'generated',a.attgenerated,'ownedSequence',(SELECT s.relname FROM pg_depend dep JOIN pg_class s ON s.oid=dep.objid WHERE dep.refobjid=a.attrelid AND dep.refobjsubid=a.attnum AND dep.classid='pg_class'::regclass AND s.relkind='S' LIMIT 1))::text FROM objects c JOIN pg_attribute a ON a.attrelid=c.oid LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum LEFT JOIN pg_collation co ON co.oid=a.attcollation WHERE c.relkind IN ('r','p') AND a.attnum>0 AND NOT a.attisdropped ORDER BY c.relname,a.attnum;
SELECT json_build_object('category','constraints','relation',c.relname,'name',con.conname,'type',con.contype,'columns',(SELECT coalesce(json_agg(a.attname ORDER BY u.ord),'[]') FROM unnest(con.conkey) WITH ORDINALITY u(attnum,ord) JOIN pg_attribute a ON a.attrelid=con.conrelid AND a.attnum=u.attnum),'referencedRelation',rc.relname,'referencedColumns',(SELECT coalesce(json_agg(a.attname ORDER BY u.ord),'[]') FROM unnest(con.confkey) WITH ORDINALITY u(attnum,ord) JOIN pg_attribute a ON a.attrelid=con.confrelid AND a.attnum=u.attnum),'matchType',con.confmatchtype,'updateAction',con.confupdtype,'deleteAction',con.confdeltype,'validated',con.convalidated,'deferrable',con.condeferrable,'initiallyDeferred',con.condeferred,'definition',pg_get_constraintdef(con.oid,true))::text FROM pg_constraint con JOIN objects c ON c.oid=con.conrelid LEFT JOIN pg_class rc ON rc.oid=con.confrelid ORDER BY c.relname,con.conname;
SELECT json_build_object('category','indexes','relation',t.relname,'name',i.relname,'unique',x.indisunique,'valid',x.indisvalid,'ready',x.indisready,'method',am.amname,'keyCount',x.indnkeyatts,'columns',(SELECT coalesce(json_agg(pg_get_indexdef(x.indexrelid,k,TRUE) ORDER BY k),'[]') FROM generate_series(1,x.indnatts) k),'operatorClasses',(SELECT coalesce(json_agg(opc.opcname ORDER BY u.ord),'[]') FROM unnest(x.indclass::oid[]) WITH ORDINALITY u(oid,ord) JOIN pg_opclass opc ON opc.oid=u.oid),'collations',(SELECT coalesce(json_agg(CASE WHEN u.oid=0 THEN NULL ELSE col.collname END ORDER BY u.ord),'[]') FROM unnest(x.indcollation::oid[]) WITH ORDINALITY u(oid,ord) LEFT JOIN pg_collation col ON col.oid=u.oid),'options',x.indoption::text,'expressions',pg_get_expr(x.indexprs,x.indrelid),'predicate',pg_get_expr(x.indpred,x.indrelid))::text FROM pg_index x JOIN objects t ON t.oid=x.indrelid JOIN pg_class i ON i.oid=x.indexrelid JOIN pg_am am ON am.oid=i.relam ORDER BY t.relname,i.relname;
SELECT json_build_object('category','sequences','name',c.relname,'dataType',format_type(s.seqtypid,NULL),'start',s.seqstart,'increment',s.seqincrement,'minimum',s.seqmin,'maximum',s.seqmax,'cache',s.seqcache,'cycle',s.seqcycle,'ownerIsCurrentUser',pg_has_role(current_user,c.relowner,'MEMBER'),'dependencyType',d.deptype,'ownedRelation',t.relname,'ownedColumn',a.attname)::text FROM pg_sequence s JOIN objects c ON c.oid=s.seqrelid LEFT JOIN pg_depend d ON d.objid=c.oid AND d.classid='pg_class'::regclass AND d.refclassid='pg_class'::regclass AND d.refobjsubid>0 LEFT JOIN pg_class t ON t.oid=d.refobjid LEFT JOIN pg_attribute a ON a.attrelid=d.refobjid AND a.attnum=d.refobjsubid ORDER BY c.relname;
SELECT json_build_object('category','triggers','relation',c.relname,'name',t.tgname,'enabled',t.tgenabled,'definition',pg_get_triggerdef(t.oid,true))::text FROM pg_trigger t JOIN objects c ON c.oid=t.tgrelid WHERE NOT t.tgisinternal ORDER BY c.relname,t.tgname;
SELECT json_build_object('category','rewriteRules','relation',c.relname,'name',r.rulename,'event',r.ev_type,'instead',r.is_instead,'enabled',r.ev_enabled,'definition',pg_get_ruledef(r.oid,true))::text FROM pg_rewrite r JOIN objects c ON c.oid=r.ev_class WHERE r.rulename<>'_RETURN' ORDER BY c.relname,r.rulename;
SELECT json_build_object('category','rls','relation',relname,'enabled',relrowsecurity,'forced',relforcerowsecurity)::text FROM objects WHERE relkind IN ('r','p') ORDER BY relname;
SELECT json_build_object('category','policies','relation',c.relname,'name',p.polname,'permissive',p.polpermissive,'roles',p.polroles::text,'command',p.polcmd,'using',pg_get_expr(p.polqual,p.polrelid),'check',pg_get_expr(p.polwithcheck,p.polrelid))::text FROM pg_policy p JOIN objects c ON c.oid=p.polrelid ORDER BY c.relname,p.polname;
SELECT json_build_object('category','inboundDependencies','sourceClass',d.classid::regclass::text,'sourceObject',d.objid,'sourceSubId',d.objsubid,'targetRelation',c.relname,'targetSubId',d.refobjsubid,'dependencyType',d.deptype)::text FROM pg_depend d JOIN objects c ON c.oid=d.refobjid LEFT JOIN objects own ON own.oid=d.objid WHERE own.oid IS NULL AND d.classid<>'pg_type'::regclass ORDER BY c.relname,d.classid::regclass::text,d.objid,d.objsubid;
SQL
  python3 - "$catalog_rows" "$output/catalog-manifest.json" <<'PY'
import json, pathlib, sys
categories=['databaseSchema','relations','columns','sequences','constraints','indexes','triggers','rewriteRules','rls','policies','inboundDependencies']
result={name:[] for name in categories}
for line in pathlib.Path(sys.argv[1]).read_text().splitlines():
    item=json.loads(line); category=item.pop('category'); result[category].append(item)
for value in result.values(): value.sort(key=lambda x: json.dumps(x,sort_keys=True,separators=(',',':')))
pathlib.Path(sys.argv[2]).write_text(json.dumps(result,sort_keys=True,separators=(',',':'))+'\n',encoding='utf-8')
PY
  raw_sha=$(sha256sum "$raw" | cut -d' ' -f1)
  normalized_sha=$(sha256sum "$output/schema.normalized.sql" | cut -d' ' -f1)
  manifest_sha=$(sha256sum "$output/catalog-manifest.json" | cut -d' ' -f1)
  family=P0
  case $index in 0|1|2|3) family=P0;; 4|5|6|7) family=P1;; 8|9) family=P2;; 10) family=P3;; 11) family=P4;; esac
  state_json=$(python3 - "$inventory" "$index" <<'PY'
import json,sys
print(json.dumps(json.load(open(sys.argv[1]))['states'][int(sys.argv[2])],separators=(',',':')))
PY
)
  python3 - "$records" "$state_json" "$family" "$raw_sha" "$normalized_sha" "$manifest_sha" "$image" "$sdk_version" "$docker_version" "$pg_dump_version" <<'PY'
import json,sys
path,state,family,raw,norm,manifest,image,sdk,docker,pgdump=sys.argv[1:]
s=json.loads(state)
s.update(family=family,variant='',postgresImageDigest=image.split('@',1)[1],dotnetSdkVersion=sdk,
 dockerServerVersion=docker,pgDumpVersion=pgdump,
 productionBootCommand='Collectify__Database__Provider=postgres Collectify__Database__ConnectionString=[REDACTED] ASPNETCORE_URLS=http://127.0.0.1:[PORT] dotnet run --project Collectify.Api --no-launch-profile',
 pgDumpCommand='pg_dump --schema-only --schema=public --no-owner --no-privileges --no-comments --quote-all-identifiers --restrict-key=COLLECTIFYISSUE100PROVENANCE',normalizationVersion='1',rawDumpSha256=raw,
 normalizedFixturePath=f"src/server/tests/Collectify.PostgresTests/Fixtures/generated/{s['commit']}/schema.normalized.sql",normalizedFixtureSha256=norm,
 catalogManifestPath=f"src/server/tests/Collectify.PostgresTests/Fixtures/generated/{s['commit']}/catalog-manifest.json",catalogManifestSha256=manifest)
with open(path,'a') as f: f.write(json.dumps(s,sort_keys=True,separators=(',',':'))+'\n')
PY
  rm -f "$raw" "$catalog_rows" "$log"
  cleanup_state
  scan_secrets
done

python3 - "$records" "$publish_root/provenance.json" "$inventory_sha" "$generator_sha" "${image#*@}" <<'PY'
import json,pathlib,sys
records=[json.loads(x) for x in pathlib.Path(sys.argv[1]).read_text().splitlines()]
seen={}
global_hash={}
for item in records:
    key=(item['normalizedFixtureSha256'],item['catalogManifestSha256'])
    family=item['family']
    if key in global_hash and global_hash[key] != family:
        raise SystemExit(f'ambiguous shape shared by lifecycle families {global_hash[key]} and {family}')
    global_hash[key]=family
    variants=seen.setdefault(family,[])
    if key not in variants: variants.append(key)
    number=variants.index(key)+1
    item['variant']=family if number==1 else f'{family}-v{number}'
result={'schemaVersion':1,'stateCount':12,'inventorySha256':sys.argv[3],
        'generatorSha256':sys.argv[4],'postgresImageDigest':sys.argv[5],'states':records}
pathlib.Path(sys.argv[2]).write_text(json.dumps(result,sort_keys=True,separators=(',',':'))+'\n',encoding='utf-8')
PY
scan_secrets
test "$(find "$publish_root" -type f | wc -l)" -eq 25
test ! -e "$generated" || rm -rf -- "$generated"
mv "$publish_root" "$generated"
publish_root=
scan_secrets
echo 'generated PostgreSQL legacy fixtures: states=12'
