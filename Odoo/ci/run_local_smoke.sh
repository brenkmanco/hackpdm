#!/usr/bin/env bash
# Local version of .github/workflows/odoo-integration.yml.
#
# Starts PostgreSQL 15 + Odoo 16.0 (hackpdm addon installed, smoke data seeded), runs
# HackPDM.Tests against it (unit + live integration tests), then launches the real
# desktop client in smoke-test mode. Containers are removed on exit unless KEEP=1.
#
# Usage: Odoo/ci/run_local_smoke.sh [--tests-only | --smoke-only]
#   ODOO_PORT=8069   host port Odoo is published on
#   KEEP=1           leave the containers running afterwards (re-runs reuse nothing; they start clean)
#   NO_BUILD=1       skip building the desktop client
#
# Needs: docker (your user in the `docker` group), python3, dotnet; xvfb-run is used when
# installed, otherwise the client window opens on your current $DISPLAY.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

MODE="${1:-all}"
ODOO_PORT="${ODOO_PORT:-8069}"
NET=hackpdm-smoke
PG=hackpdm-smoke-pg
ODOO=hackpdm-smoke-odoo
ODOO_IMAGE=hackpdm-odoo:16.0
export ODOO_DB=hackpdm_ci ODOO_USER=admin ODOO_PASSWORD=admin
export ODOO_URL="http://localhost:${ODOO_PORT}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RESULTS="$ROOT/TestResults"
mkdir -p "$RESULTS"

log() { printf '\n\033[1m== %s\033[0m\n' "$*"; }

cleanup() {
	local code=$?
	if [[ "${KEEP:-0}" != 1 ]]; then
		log "Removing containers"
		docker rm -f "$ODOO" "$PG" >/dev/null 2>&1 || true
		docker network rm "$NET" >/dev/null 2>&1 || true
	else
		log "KEEP=1: Odoo left running at $ODOO_URL (docker rm -f $ODOO $PG; docker network rm $NET)"
	fi
	exit "$code"
}
trap cleanup EXIT

docker info >/dev/null 2>&1 || { echo "docker is not reachable (is your user in the docker group? log out/in after adding it)"; exit 2; }

log "Building Odoo image"
docker build -q -t "$ODOO_IMAGE" Odoo/ci >/dev/null

log "Starting PostgreSQL"
docker rm -f "$ODOO" "$PG" >/dev/null 2>&1 || true
docker network rm "$NET" >/dev/null 2>&1 || true
docker network create "$NET" >/dev/null
docker run -d --name "$PG" --network "$NET" \
	-e POSTGRES_USER=odoo -e POSTGRES_PASSWORD=odoo -e POSTGRES_DB=postgres \
	postgres:15 >/dev/null
for _ in $(seq 1 60); do
	docker exec "$PG" pg_isready -U odoo >/dev/null 2>&1 && break
	sleep 1
done
docker exec "$PG" pg_isready -U odoo >/dev/null

ODOO_ENV=(-e HOST="$PG" -e USER=odoo -e PASSWORD=odoo -v "$ROOT/Odoo/addons:/mnt/extra-addons:ro")

log "Installing hackpdm addon into $ODOO_DB"
docker run --rm --network "$NET" "${ODOO_ENV[@]}" "$ODOO_IMAGE" \
	odoo -d "$ODOO_DB" -i hackpdm --without-demo=all --stop-after-init >"$RESULTS/odoo-install.log" 2>&1 \
	|| { tail -50 "$RESULTS/odoo-install.log"; exit 1; }
state=$(docker exec "$PG" psql -U odoo -d "$ODOO_DB" -tAc "select state from ir_module_module where name = 'hackpdm'")
echo "hackpdm module state: $state"
[[ "$state" == installed ]]

log "Running the hackpdm addon's server tests"
# post_install TransactionCases, in a database of their own: their rows are rolled back but the ids
# they used are not, and the client needs the seeded root folder to be hp.directory 1
docker run --rm --network "$NET" "${ODOO_ENV[@]}" "$ODOO_IMAGE" \
	odoo -d "${ODOO_DB}_tests" -i hackpdm --without-demo=all --test-enable --test-tags /hackpdm --stop-after-init >"$RESULTS/odoo-tests.log" 2>&1 \
	|| { grep -E "FAIL|ERROR" "$RESULTS/odoo-tests.log" | tail -40; echo "addon tests failed (see $RESULTS/odoo-tests.log)"; exit 1; }
for suite in TestSafeguards TestServerFunctions; do grep -q "Starting $suite" "$RESULTS/odoo-tests.log" || { echo "addon tests $suite did not run"; exit 1; }; done
! grep -E "^.* (ERROR|CRITICAL) .*(FAIL|ERROR):" "$RESULTS/odoo-tests.log" || exit 1
grep -E "tests? .*(passed|failed)|of [0-9]+ tests" "$RESULTS/odoo-tests.log" | tail -2 || true

log "Starting Odoo on $ODOO_URL"
docker run -d --name "$ODOO" --network "$NET" -p "${ODOO_PORT}:8069" "${ODOO_ENV[@]}" "$ODOO_IMAGE" \
	odoo -d "$ODOO_DB" --db-filter="^${ODOO_DB}\$" >/dev/null
for _ in $(seq 1 60); do
	curl -fsS "$ODOO_URL/web/health" >/dev/null 2>&1 && break
	sleep 2
done
curl -fsS "$ODOO_URL/web/health" >/dev/null || { docker logs "$ODOO" | tail -100; exit 1; }

log "Seeding smoke data"
python3 Odoo/ci/seed_smoke_data.py "$ODOO_URL" "$ODOO_DB" "$ODOO_USER" "$ODOO_PASSWORD"

status=0
if [[ "$MODE" != --smoke-only ]]; then
	log "Running HackPDM.Tests against Odoo"
	dotnet test Src/HackPDM.Tests/HackPDM.Tests.csproj --logger "console;verbosity=minimal" || status=1
fi

if [[ "$MODE" != --tests-only ]]; then
	if [[ "${NO_BUILD:-0}" != 1 ]]; then
		log "Building desktop client"
		dotnet build Src/HackPDM/HackPDM.csproj -f net10.0-desktop -p:Platform=x64 -c Debug -v q -nologo
	fi
	log "Client smoke test"
	export HACKPDM_SMOKE_TEST=1 HACKPDM_ODOO_ADDRESS=localhost HACKPDM_ODOO_PORT="$ODOO_PORT" \
		HACKPDM_ODOO_DB="$ODOO_DB" HACKPDM_ODOO_USER="$ODOO_USER" HACKPDM_ODOO_PASSWORD="$ODOO_PASSWORD" \
		HACKPDM_SMOKE_DIRECTORY=Parts HACKPDM_SMOKE_MIN_ENTRIES=3 \
		HACKPDM_SMOKE_REPORT="$RESULTS/smoke-report.json" HACKPDM_SMOKE_TIMEOUT=240
	# settings, data and the saved login go to a throw-away folder, not your own settings and keychain
	SMOKE_HOME=$(mktemp -d)
	export HACKPDM_CONFIG_DIR="$SMOKE_HOME/config" HACKPDM_DATA_DIR="$SMOKE_HOME/data" HACKPDM_CREDENTIAL_STORE=file
	APP=(dotnet Src/HackPDM/bin/x64/Debug/net10.0-desktop/HackPDM.dll)
	set +e
	if command -v xvfb-run >/dev/null; then
		timeout 300 xvfb-run -a -s "-screen 0 1600x1000x24" "${APP[@]}" 2>&1 | tee "$RESULTS/smoke.log"
	else
		echo "(xvfb-run not installed: using DISPLAY=${DISPLAY:-:0}, a window will open)"
		DISPLAY="${DISPLAY:-:0}" timeout 300 "${APP[@]}" 2>&1 | tee "$RESULTS/smoke.log"
	fi
	smoke=${PIPESTATUS[0]}
	rm -rf "$SMOKE_HOME"
	set -e
	echo "smoke exit code: $smoke"
	[[ -f "$RESULTS/smoke-report.json" ]] && cat "$RESULTS/smoke-report.json"
	[[ "$smoke" == 0 ]] || status=1
fi

docker logs "$ODOO" >"$RESULTS/odoo-server.log" 2>&1 || true
log "Done (status $status). Logs in $RESULTS"
exit "$status"
