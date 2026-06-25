# Sensor Simulator

A small Python tool that **fakes the 4 basin sensors** for development. It
publishes to the same MQTT topics and JSON format the ESP32 firmware uses, so
the backend can be developed and tested without any real hardware.

## What it publishes

One message per basin, per cycle:

| | |
|---|---|
| **Topics** | `hydrolitics/basin1` … `hydrolitics/basin4` |
| **Reading** | `{"distance_cm":25.0,"depth_cm":35.0,"percent":70.0,"litres":210.0}` |
| **Error** | `{"error":"no_echo"}` (injected at `--error-rate`, default 2%) |

Each basin keeps a water level that **drifts via a random walk**, and the
`distance` / `percent` / `litres` are all derived from that one depth using the
**same formula as the firmware** — so the values stay self-consistent and look
like real, slowly-changing readings. The occasional `no_echo` error exercises
the backend's error-handling branch.

## Configuration

Broker connection is read from these settings, in priority order:

1. **Real environment variables** (how the Docker container is configured)
2. **The repo-root `.env`** (`Mqtt__Host`, `Mqtt__Port`, `Mqtt__Username`,
   `Mqtt__Password`) — found by searching upward, so it works when run locally
3. **CLI flags** (override everything)

It reuses the same `Mqtt__*` values as the backend, so there are no separate or
hardcoded credentials.

## Running with Docker (recommended)

It's wired into the root `docker-compose.yml` as the `sensor-simulator` service.

```bash
# from the repo root
docker compose up sensor-simulator           # just the simulator
docker compose up                            # the whole stack (InfluxDB + simulator)
docker compose up -d sensor-simulator        # detached
docker compose logs -f sensor-simulator      # watch its output
docker compose down                          # stop everything
```

The publish interval is set by the compose `command` (default `--interval 8`).
Change it in `docker-compose.yml` and re-run.

> Note: the simulator publishes to the broker at `Mqtt__Host` (your LAN broker,
> e.g. `192.168.1.25`). The container reaches it over your LAN — it does **not**
> run a broker itself.

## Running locally (without Docker)

```bash
pip install -r requirements.txt        # or: py -m pip install -r requirements.txt
python mqtt_fake_sensors.py            # or: py mqtt_fake_sensors.py
```

Run from anywhere in the repo — it searches upward for the root `.env`.

## CLI flags

| Flag | Default | Purpose |
|------|---------|---------|
| `--interval` | `1.0` | Seconds between publish cycles |
| `--error-rate` | `0.02` | Probability (0–1) a basin sends `no_echo` instead of a reading |
| `--host` | from `.env` | Broker host (override) |
| `--port` | from `.env` | Broker port (override) |
| `--username` / `--password` | from `.env` | Broker credentials (override) |

Examples:
```bash
python mqtt_fake_sensors.py --interval 10        # slower
python mqtt_fake_sensors.py --error-rate 0.2     # stress the error path
python mqtt_fake_sensors.py --error-rate 0       # clean data only
```

## Basin geometry

The per-basin dimensions (length, width, full depth, sensor gap) are defined in
the `BASINS` dict at the top of `mqtt_fake_sensors.py`. Edit them there to change
the simulated tank sizes. `sensor_gap` is kept ≥ 25 cm to mirror the real
sensor's blind zone.
