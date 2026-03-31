#!/usr/bin/env bash

set -euo pipefail

APP_ROOT="/opt/einsatzueberwachung"
BACKUP_ROOT="/opt/einsatzueberwachung/backups"
SERVER_DATA_DIR="$APP_ROOT/server"
APP_DATA_DIR="$APP_ROOT/data"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
TMP_DIR="$BACKUP_ROOT/tmp-$TIMESTAMP"
ARCHIVE="$BACKUP_ROOT/einsatz-backup-$TIMESTAMP.tar.gz"

mkdir -p "$BACKUP_ROOT" "$TMP_DIR"

# Runtime-Dateien sichern (inklusive DB, falls im App-Verzeichnis)
if [[ -d "$SERVER_DATA_DIR" ]]; then
  rsync -a --delete \
    --exclude "logs/" \
    --exclude "wwwroot/" \
    --exclude "*.log" \
    "$SERVER_DATA_DIR/" "$TMP_DIR/server/"
fi

if [[ -d "$APP_DATA_DIR" ]]; then
  rsync -a "$APP_DATA_DIR/" "$TMP_DIR/data/"
fi

# Optional bekannte SQLite-Dateien explizit sichern
find "$APP_ROOT" -maxdepth 4 -type f \( -name "*.db" -o -name "*.sqlite" -o -name "*.sqlite3" \) -print0 \
  | while IFS= read -r -d '' db_file; do
      rel_path="${db_file#${APP_ROOT}/}"
      mkdir -p "$TMP_DIR/databases/$(dirname "$rel_path")"
      cp "$db_file" "$TMP_DIR/databases/$rel_path"
    done

# Metadaten für Restore-Dokumentation
cat > "$TMP_DIR/backup-meta.txt" <<EOF
created_at=$TIMESTAMP
host=$(hostname)
retention_days=$RETENTION_DAYS
EOF

tar -C "$TMP_DIR" -czf "$ARCHIVE" .
rm -rf "$TMP_DIR"

# Aufbewahrung bereinigen
find "$BACKUP_ROOT" -maxdepth 1 -type f -name "einsatz-backup-*.tar.gz" -mtime +"$RETENTION_DAYS" -delete

echo "Backup erstellt: $ARCHIVE"
