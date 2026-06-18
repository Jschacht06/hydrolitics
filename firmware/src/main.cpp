#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include "secrets.h" // copy secrets.example.h -> secrets.h and fill in (gitignored)


struct Basin {
  const char *name;
  const char *mqttTopic; // where this basin's JSON is published
  uint8_t trigPin;
  uint8_t echoPin;

  float lengthCm;     // basin length
  float widthCm;      // basin width
  float fullDepthCm;  // water depth at 100% full
  float sensorGapCm;  // air gap between sensor face and the max fill line (>= 25, blind zone)
};

Basin basins[] = {
  // name        topic                  trig echo  length  width  fullDepth  sensorGap
  { "Basin 1", "hydrolitics/basin1",      5,  18,   100.0,  60.0,    50.0,     10.0 },
  { "Basin 2", "hydrolitics/basin2",     19,  21,   100.0,  60.0,    50.0,     10.0 },
  { "Basin 3", "hydrolitics/basin3",     22,  23,   100.0,  60.0,    50.0,     10.0 },
  { "Basin 4", "hydrolitics/basin4",     25,  26,   100.0,  60.0,    50.0,     10.0 },
};

const uint8_t BASIN_COUNT = sizeof(basins) / sizeof(basins[0]);

// Measurement tuning.
const uint8_t  SAMPLES_PER_READING = 5;       // median of N pings (rejects spikes)
const uint32_t ECHO_TIMEOUT_US     = 30000;   // ~5 m max range before giving up
const float    SOUND_CM_PER_US     = 0.0343f; // speed of sound at ~20 C
const uint32_t LOOP_INTERVAL_MS    = 1000;

WiFiClient wifiClient;
PubSubClient mqtt(wifiClient);

// Result of one basin measurement cycle.
struct Reading {
  bool valid;        // false if the sensor gave no echo
  float distanceCm;
  float depthCm;
  float percent;
  float litres;
};

// ---------------------------------------------------------------------------

// Returns the distance to the surface in cm, or NAN if the ping timed out.
float pingDistanceCm(const Basin &b) {
  digitalWrite(b.trigPin, LOW);
  delayMicroseconds(2);
  digitalWrite(b.trigPin, HIGH);
  delayMicroseconds(10);
  digitalWrite(b.trigPin, LOW);

  uint32_t echoUs = pulseIn(b.echoPin, HIGH, ECHO_TIMEOUT_US);
  if (echoUs == 0) {
    return NAN; // no echo within timeout
  }
  // Round trip -> one way distance.
  return (echoUs * SOUND_CM_PER_US) / 2.0f;
}

// Median of several pings to smooth out noise. Returns NAN if all pings fail.
float readDistanceCm(const Basin &b) {
  float samples[SAMPLES_PER_READING];
  uint8_t valid = 0;

  for (uint8_t i = 0; i < SAMPLES_PER_READING; i++) {
    float d = pingDistanceCm(b);
    if (!isnan(d)) {
      samples[valid++] = d;
    }
    delay(60); // JSN-SR04T needs settle time between pings
  }

  if (valid == 0) {
    return NAN;
  }

  // Simple insertion sort, then take the middle element.
  for (uint8_t i = 1; i < valid; i++) {
    float key = samples[i];
    int8_t j = i - 1;
    while (j >= 0 && samples[j] > key) {
      samples[j + 1] = samples[j];
      j--;
    }
    samples[j + 1] = key;
  }
  return samples[valid / 2];
}

// Measure a basin and convert the distance into depth / percent / litres.
Reading computeReading(const Basin &b) {
  Reading r = {};
  float distance = readDistanceCm(b);

  if (isnan(distance)) {
    r.valid = false;
    return r;
  }

  float waterDepth = b.fullDepthCm + b.sensorGapCm - distance;
  if (waterDepth < 0)             waterDepth = 0;
  if (waterDepth > b.fullDepthCm) waterDepth = b.fullDepthCm;

  r.valid      = true;
  r.distanceCm = distance;
  r.depthCm    = waterDepth;
  r.percent    = (waterDepth / b.fullDepthCm) * 100.0f;
  r.litres     = (b.lengthCm * b.widthCm * waterDepth) / 1000.0f;
  return r;
}

/* for debugging

void printReading(const Basin &b, const Reading &r) {
  if (!r.valid) {
    Serial.printf("%-8s | no echo (sensor error / out of range)\n", b.name);
    return;
  }
  Serial.printf("%-8s | dist %6.1f cm | depth %6.1f cm | %5.1f %% | %7.1f L\n",
                b.name, r.distanceCm, r.depthCm, r.percent, r.litres);
}

*/

void publishReading(const Basin &b, const Reading &r) {
  if (!mqtt.connected()) {
    return;
  }

  char payload[160];
  if (r.valid) {
    snprintf(payload, sizeof(payload),
             "{\"distance_cm\":%.1f,\"depth_cm\":%.1f,\"percent\":%.1f,\"litres\":%.1f}",
             r.distanceCm, r.depthCm, r.percent, r.litres);
  } else {
    snprintf(payload, sizeof(payload), "{\"error\":\"no_echo\"}");
  }
  mqtt.publish(b.mqttTopic, payload);
}

// ---------------------------------------------------------------------------

void connectWiFi() {
  if (WiFi.status() == WL_CONNECTED) {
    return;
  }
  Serial.printf("WiFi: connecting to %s ...\n", WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);

  uint32_t start = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - start < 15000) {
    delay(250);
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.printf("WiFi: connected, IP %s\n", WiFi.localIP().toString().c_str());
  } else {
    Serial.println("WiFi: connect failed, will retry");
  }
}

// Make sure WiFi + MQTT are up; attempt a single reconnect if not.
void ensureConnected() {
  connectWiFi();
  if (WiFi.status() != WL_CONNECTED || mqtt.connected()) {
    return;
  }

  Serial.printf("MQTT: connecting to %s:%u ...\n", MQTT_HOST, MQTT_PORT);
  bool ok = (strlen(MQTT_USER) > 0)
                ? mqtt.connect(MQTT_CLIENT_ID, MQTT_USER, MQTT_PASS)
                : mqtt.connect(MQTT_CLIENT_ID);
  Serial.println(ok ? "MQTT: connected" : "MQTT: connect failed, will retry");
}

void setup() {
  Serial.begin(115200);
  delay(200);

  for (uint8_t i = 0; i < BASIN_COUNT; i++) {
    pinMode(basins[i].trigPin, OUTPUT);
    pinMode(basins[i].echoPin, INPUT);
    digitalWrite(basins[i].trigPin, LOW);
  }

  mqtt.setServer(MQTT_HOST, MQTT_PORT);
  connectWiFi();
  ensureConnected();

  Serial.println("\nHydrolitics - 4 basin level monitor ready.\n");
}

void loop() {
  ensureConnected();
  mqtt.loop();

  for (uint8_t i = 0; i < BASIN_COUNT; i++) {
    Reading r = computeReading(basins[i]);
    // printReading(basins[i], r);
    publishReading(basins[i], r);
  }
 // Serial.println();
  delay(LOOP_INTERVAL_MS);
}
