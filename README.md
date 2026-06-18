# Hydrolitics

**A smart and scalable water level sensing system.**

Hydrolitics lets you monitor the water level of multiple containers remotely — no
need to open them or check them by hand. Just glance at your phone or PC and see
exactly how much water each container holds, in real time.

## The Idea

The project started with a simple problem: I have four large containers holding
water, and I wanted to know how full each one is without having to open them.

The solution is a sensor-to-dashboard pipeline:

- **Sensing** — A [JSN-SR04T](https://www.google.com/search?q=JSN-SR04T) ultrasonic
  sensor is mounted inside each container, facing down toward the water surface. It
  measures the distance to the water, which can be converted into a water level (and
  volume).
- **Processing** — All sensors connect to an **ESP32**, which reads the raw distance
  values and converts them into a meaningful water level for each container.
- **Transport** — The ESP32 sends these readings to a backend over an MQTT server.
- **Visualization** — A locally deployable website receives the data and presents it
  through a **dashboard**, where you can see the current water
  level of every container.

## Goals

- **Smart** — Turn raw distance readings into clear, useful water level information.
- **Scalable** — Built to grow beyond the initial four containers.
- **Convenient** — Check your water levels from anywhere with a browser.

