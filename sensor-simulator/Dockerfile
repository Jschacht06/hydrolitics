FROM python:3.13-slim

#flush stdout
ENV PYTHONUNBUFFERED=1

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY mqtt_fake_sensors.py .

ENTRYPOINT ["python", "mqtt_fake_sensors.py"]
