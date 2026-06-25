#!/usr/bin/env python3
import argparse
import json
import os
import random
import signal
import sys
import time
from pathlib import Path

try:
    import paho.mqtt.client as mqtt
except ImportError:
    sys.exit("paho-mqtt is not installed. Run:  pip install paho-mqtt")


# --- Basin geometry (matches the firmware's per-basin config) ----------------
# sensor_gap is kept >= 25 cm to stay clear of the JSN-SR04T blind zone.
BASINS = {
    "basin1": {"length": 100.0, "width": 60.0, "full_depth": 50.0, "sensor_gap": 30.0},
    "basin2": {"length": 120.0, "width": 80.0, "full_depth": 60.0, "sensor_gap": 30.0},
    "basin3": {"length": 80.0,  "width": 50.0, "full_depth": 40.0, "sensor_gap": 28.0},
    "basin4": {"length": 150.0, "width": 100.0, "full_depth": 70.0, "sensor_gap": 35.0},
}

TOPIC_PREFIX = "hydrolitics"

 
def find_env_file(start: Path): #search .env
    for directory in [start, *start.parents]:
        candidate = directory / ".env"
        if candidate.exists():
            return candidate
    return None


def load_dotenv(path) -> dict: # read env
    values = {}
    if path is None or not path.exists():
        return values
    for line in path.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        values[key.strip()] = value.rstrip("\r\n").strip()
    return values


def get_setting(key: str, env_file: dict, default=None):
    return os.environ.get(key, env_file.get(key, default))


def build_reading(basin_id: str, depth_cm: float) -> dict:
    cfg = BASINS[basin_id]
    distance = cfg["sensor_gap"] + (cfg["full_depth"] - depth_cm)
    percent = (depth_cm / cfg["full_depth"]) * 100.0
    litres = (cfg["length"] * cfg["width"] * depth_cm) / 1000.0
    return {
        "distance_cm": round(distance, 1),
        "depth_cm": round(depth_cm, 1),
        "percent": round(percent, 1),
        "litres": round(litres, 1),
    }


def make_client(client_id: str): #create mqtt client
    try:
        return mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, client_id=client_id)
    except (AttributeError, TypeError):
        return mqtt.Client(client_id=client_id)  # paho-mqtt 1.x


def main() -> None:
    env_file = load_dotenv(find_env_file(Path(__file__).resolve().parent))

    parser = argparse.ArgumentParser(description="Publish fake basin sensor data over MQTT.")
    parser.add_argument("--host", default=get_setting("Mqtt__Host", env_file, "localhost"))
    parser.add_argument("--port", type=int, default=int(get_setting("Mqtt__Port", env_file, 1883)))
    parser.add_argument("--username", default=get_setting("Mqtt__Username", env_file, ""))
    parser.add_argument("--password", default=get_setting("Mqtt__Password", env_file, ""))
    parser.add_argument("--interval", type=float, default=1.0, help="seconds between publish cycles")
    parser.add_argument("--error-rate", type=float, default=0.02,
                        help="probability (0-1) a basin sends {'error':'no_echo'} instead of a reading")
    args = parser.parse_args()

    client = make_client("hydrolitics-faker")
    if args.username:
        client.username_pw_set(args.username, args.password)

    print(f"Connecting to {args.host}:{args.port} ...")
    client.connect(args.host, args.port, keepalive=30)
    client.loop_start()

    # Each basin starts around half full and drifts via a random walk.
    depths = {b: BASINS[b]["full_depth"] * 0.5 for b in BASINS}

    running = True

    def stop(_sig, _frame):
        nonlocal running
        running = False

    signal.signal(signal.SIGINT, stop)
    signal.signal(signal.SIGTERM, stop)

    print(f"Publishing every {args.interval}s (error rate {args.error_rate:.0%}). Ctrl+C to stop.\n")
    try:
        while running:
            for basin_id, cfg in BASINS.items():
                topic = f"{TOPIC_PREFIX}/{basin_id}"

                if random.random() < args.error_rate:
                    payload = {"error": "no_echo"}
                else:
                    # Random walk the water level, clamped to the basin.
                    step = random.uniform(-1.5, 1.5)
                    depths[basin_id] = max(0.0, min(cfg["full_depth"], depths[basin_id] + step))
                    payload = build_reading(basin_id, depths[basin_id])

                client.publish(topic, json.dumps(payload))
                print(f"{topic}  {json.dumps(payload)}")

            print("-" * 40)
            time.sleep(args.interval)
    finally:
        client.loop_stop()
        client.disconnect()
        print("\nStopped.")


if __name__ == "__main__":
    main()
