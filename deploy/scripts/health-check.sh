#!/usr/bin/env bash

set -euo pipefail

SERVER_HEALTH_URL="${SERVER_HEALTH_URL:-http://127.0.0.1:5000/health}"
MOBILE_HEALTH_URL="${MOBILE_HEALTH_URL:-http://127.0.0.1:5001/health}"
LOG_TAG="einsatz-health-check"

check_and_recover() {
  local service_name="$1"
  local health_url="$2"

  if ! curl --silent --show-error --fail --max-time 10 "$health_url" >/dev/null; then
    logger -t "$LOG_TAG" "Healthcheck fehlgeschlagen fuer $service_name ($health_url). Neustart wird ausgefuehrt."
    systemctl restart "$service_name"
    sleep 5

    if ! curl --silent --show-error --fail --max-time 10 "$health_url" >/dev/null; then
      logger -t "$LOG_TAG" "Healthcheck nach Neustart weiterhin fehlerhaft fuer $service_name."
      return 1
    fi

    logger -t "$LOG_TAG" "Dienst $service_name erfolgreich wiederhergestellt."
  fi

  return 0
}

check_and_recover "einsatzueberwachung-server.service" "$SERVER_HEALTH_URL"
check_and_recover "einsatzueberwachung-mobile.service" "$MOBILE_HEALTH_URL"
