#!/usr/bin/env bash
# Nightly encrypted Postgres backup to Backblaze B2. Installed by cloud-init
# (terraform/modules/vps_app) and run by amrgp-backup.timer. To run it by hand:
#   sudo systemctl start amrgp-backup && journalctl -u amrgp-backup
# Environment (from /etc/amrgp/backup.env): B2_BUCKET, AGE_RECIPIENT, RCLONE_CONFIG_B2_*
#
# Dumps go to daily/ and, on the 1st of each month, also monthly/; the bucket's
# lifecycle rules expire them. The B2 key can't delete files, but writeFiles also
# allows hiding them, which the lifecycle rules would then delete; the bucket's
# Object Lock (Compliance, 14 days) is what keeps a compromised server from
# erasing recent backups. Restore with:
#   age --decrypt -i backup-key.txt db-<date>.dump.age | pg_restore --clean --if-exists -d <db>
set -euo pipefail

stamp=$(date -u +%F)
tmp=$(mktemp /var/tmp/amrgp-backup.XXXXXX)
trap 'rm -f "$tmp"' EXIT

# pg_dump's custom format is already compressed
cd /opt/amrgp
docker compose -f docker-compose.prod.yml exec -T db \
  sh -c 'pg_dump --format=custom --username="$POSTGRES_USER" "$POSTGRES_DB"' \
  | age --recipient "$AGE_RECIPIENT" > "$tmp"

rclone copyto "$tmp" "b2:$B2_BUCKET/daily/db-$stamp.dump.age"
if [ "$(date -u +%d)" = "01" ]; then
  rclone copyto "$tmp" "b2:$B2_BUCKET/monthly/db-$stamp.dump.age"
fi

echo "Backed up db-$stamp.dump.age ($(stat -c %s "$tmp") bytes)"
