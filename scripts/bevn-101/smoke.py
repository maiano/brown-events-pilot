"""Measure SQL through complete legacy conference sessions HTTP responses.

Target database
    DATABASE = "bevn101" is hardcoded and has no environment override to
    prevent accidental dev/prod targeting. If the test database name changes,
    edit DATABASE in this file and update the guard in seed.sql to match.

Prerequisites
    Python 3 and a running Docker daemon. Each run generates a unique
    bevn101-<random> prefix for its network and containers. PostgreSQL uses
    the suffix -db and the API containers use role or fixture suffixes.

Usage from the repository root
    docker build -f backend/BrownEvents.Api/Dockerfile -t bevn101-api:local backend
    python3 scripts/bevn-101/smoke.py bevn101-api:local /tmp/bevn101-evidence --full-baseline

Safety and reruns
    The script creates a private Docker network, a PostgreSQL 14 container
    with tmpfs storage, and disposable API containers. It attempts to remove
    those containers and the network on exit. Existing dev/prod databases,
    containers, and volumes are outside the run. JSON and logs remain in the
    output directory. Use a path outside the repo to avoid staging dumps.
    Reruns use fresh containers and database state. Supply a new output
    directory each time because the script refuses to overwrite one.
"""

import argparse
import json
import re
import secrets
import subprocess
import time
import urllib.request
from pathlib import Path


DATABASE = "bevn101"
PREFIX = re.compile(r"^BEVN101\|([^|]+)\|(\d+)\|([^|]*)\|([^|]*)\| (.*)$")
STATEMENT = re.compile(r"^LOG:\s+(?:statement|execute [^:]+):\s+(.*)$")


def docker(*args, sql=None):
    result = subprocess.run(
        ["docker", *args], input=sql, stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT, text=True, timeout=30,
    )
    if result.returncode:
        # Container arguments may include the disposable connection password.
        raise RuntimeError(f"docker {args[0]} failed: {result.stdout.strip()}")
    return result.stdout


def wait_for(read, ready):
    deadline = time.monotonic() + 25
    while time.monotonic() < deadline:
        value = read()
        if ready(value):
            return value
        time.sleep(0.2)
    raise RuntimeError(f"Timed out waiting for {read.__name__}")


def statements(log):
    records = []
    current = None
    for line in log.splitlines():
        match = PREFIX.match(line)
        if match:
            current = None
            timestamp, pid, application, database, message = match.groups()
            statement = STATEMENT.match(message)
            if statement:
                current = dict(timestamp=timestamp, pid=int(pid), application=application,
                               database=database, sql=statement[1])
                records.append(current)
        elif current is not None and line.startswith(("\t", " ")):
            current["sql"] += "\n" + line
    return records


def app_finished(log, requests):
    events = (line for line in log.splitlines() if line.startswith("{"))
    return sum(
        event.get("Category") == "Microsoft.AspNetCore.Hosting.Diagnostics"
        and event.get("EventId") == 2
        for event in map(json.loads, events)
    ) == requests


def measure(image, output, full_baseline):
    token = f"bevn101-{secrets.token_hex(4)}"
    database_container = f"{token}-db"
    password = secrets.token_hex(16)
    containers = []
    report = {"api_image": image, "requests": []}

    def psql(application, sql):
        return docker(
            "exec", "-i", "-e", f"PGAPPNAME={application}", database_container,
            "psql", "-X", "-A", "-t", "-v", "ON_ERROR_STOP=1", "-U", "postgres",
            "-d", DATABASE, sql=sql,
        )

    def db_logs():
        return docker("logs", database_container)

    def start_api(application):
        containers.append(application)
        connection = (
            f"Host={database_container};Database={DATABASE};Username=postgres;"
            f"Password={password};Application Name={application}"
        )
        docker(
            "run", "-d", "--name", application, "--network", token,
            "-p", "127.0.0.1::80",
            "-e", "ASPNETCORE_URLS=http://+:80",
            "-e", "ASPNETCORE_ENVIRONMENT=Production",
            "-e", f"ConnectionStrings__DefaultConnection={connection}",
            "-e", "Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information",
            "-e", "Logging__LogLevel__Microsoft.AspNetCore.Hosting.Diagnostics=Information",
            "-e", "Logging__Console__FormatterName=json", image,
        )

        def api_logs():
            return docker("logs", application)

        wait_for(api_logs, lambda log: "Now listening on:" in log)
        address = docker("port", application, "80/tcp").strip()
        return f"http://{address}", api_logs

    docker("network", "create", token)
    try:
        containers.append(database_container)
        docker(
            "run", "-d", "--name", database_container, "--network", token,
            "--tmpfs", "/var/lib/postgresql/data",
            "-e", f"POSTGRES_DB={DATABASE}", "-e", f"POSTGRES_PASSWORD={password}",
            "postgres:14", "-c", "log_line_prefix=BEVN101|%m|%p|%a|%d| ",
        )

        def database_ready():
            try:
                return docker("exec", database_container, "pg_isready", "-h", "127.0.0.1")
            except RuntimeError:
                return ""

        wait_for(database_ready, lambda status: "accepting connections" in status)
        bootstrap = f"{token}-bootstrap"
        start_api(bootstrap)
        docker("stop", bootstrap)
        psql(f"{token}-seed", Path(__file__).with_name("seed.sql").read_text())
        report["postgres_version"] = psql(f"{token}-control", "SHOW server_version;").strip()

        application = f"{token}-ef-check"
        address, api_logs = start_api(application)
        with urllib.request.urlopen(f"{address}/api/conferences/10100/sessions", timeout=15) as response:
            if response.status != 200 or json.load(response) != {"data": []}:
                raise RuntimeError("EF logging check did not return the expected empty response")
        ef_log = wait_for(api_logs, lambda log: app_finished(log, 1))
        (output / "ef-check.log").write_text(ef_log)
        report["ef_command_logging_observed"] = "Microsoft.EntityFrameworkCore.Database.Command" in ef_log
        docker("stop", application)

        # Enable logging only after the EF console check, on this test instance.
        control = f"{token}-control"
        psql(control, "ALTER SYSTEM SET log_statement = 'all';")
        psql(control, "SELECT pg_reload_conf();")
        wait_for(lambda: psql(control, "SHOW log_statement;"), lambda value: value.strip() == "all")

        cases = [("0", 10100, 0), ("1", 10101, 1)]
        if full_baseline:
            cases.extend([("10", 10110, 10), ("50", 10150, 50), ("unknown", 19999, 0)])
        for case, conference_id, size in cases:
            application = f"{token}-api-{case}"
            address, api_logs = start_api(application)
            for request_number, temperature in enumerate(("cold", "warm"), start=1):
                trial = f"{case}-{temperature}"
                begin = f"SELECT '{trial}-begin';"
                end = f"SELECT '{trial}-end';"
                unrelated = f"SELECT '{trial}-unrelated';"
                psql(control, begin)
                psql(control, unrelated)
                with urllib.request.urlopen(
                    f"{address}/api/conferences/{conference_id}/sessions", timeout=30
                ) as response:
                    status = response.status
                    body = response.read()
                wait_for(api_logs, lambda log: app_finished(log, request_number))
                psql(control, end)
                log = wait_for(db_logs, lambda value: end in value)
                records = statements(log)
                markers = [index for index, entry in enumerate(records)
                           if entry["application"] == control and entry["sql"] in (begin, end)]
                if len(markers) != 2 or [records[index]["sql"] for index in markers] != [begin, end]:
                    raise RuntimeError(f"Missing or duplicated SQL markers for {trial}")
                window = records[markers[0] + 1:markers[1]]
                queries = [entry for entry in window
                           if entry["application"] == application and entry["database"] == DATABASE]
                excluded = [entry for entry in window if entry not in queries]
                startup = [entry for entry in records[:markers[0]] if entry["application"] == application]
                if request_number == 1 and not any("FROM conferences" in row["sql"] for row in startup):
                    raise RuntimeError(f"Could not verify startup SQL outside the {trial} window")
                if len(excluded) != 1 or excluded[0]["application"] != control or excluded[0]["sql"] != unrelated:
                    raise RuntimeError(f"Unexpected unrelated activity inside the {trial} window")
                if not any("FROM sessions AS" in row["sql"] and '"ConferenceId" = $' in row["sql"]
                           for row in queries):
                    raise RuntimeError(f"Missing the expected sessions SELECT for {trial}")
                response_count = len(json.loads(body)["data"])
                if status != 200 or response_count != size:
                    raise RuntimeError(f"Unexpected HTTP response for {trial}")
                (output / f"{trial}.json").write_bytes(body)
                # DISCARD ALL resets pooled connections and is counted separately from SELECTs.
                report["requests"].append(dict(
                    trial=trial, conference_id=conference_id, status=status,
                    response_session_count=response_count, statement_count=len(queries),
                    select_count=sum(row["sql"].startswith("SELECT ") for row in queries),
                    discard_all_count=sum(row["sql"] == "DISCARD ALL" for row in queries),
                    statements=queries,
                    excluded=excluded, bounds=[records[index] for index in markers],
                    startup_statement_count=len(startup) if request_number == 1 else None,
                ))
            docker("stop", application)
        (output / "postgres.log").write_text(db_logs())
        (output / "summary.json").write_text(json.dumps(report, indent=2) + "\n")
        print(json.dumps(report, indent=2))
    finally:
        for container in reversed(containers):
            subprocess.run(["docker", "rm", "-f", container], check=False, capture_output=True)
        subprocess.run(["docker", "network", "rm", token], check=False, capture_output=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", help="API Docker image to run")
    parser.add_argument("output", type=Path, help="new directory for logs and response bodies")
    parser.add_argument("--full-baseline", action="store_true", help="include 10, 50 and unknown cases")
    args = parser.parse_args()
    args.output.mkdir()
    measure(args.image, args.output, args.full_baseline)
