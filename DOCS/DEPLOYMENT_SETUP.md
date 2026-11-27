# Deployment Setup Runbook

One-time steps to take the deployment code in this repo live. The design and its reasoning are in [DEPLOYMENT_PLAN.md](../DEPLOYMENT_PLAN.md). Work top to bottom: sandbox first, then production.

Account sign-ups should use a club-owned email address, not a personal one, so the club keeps access if volunteers change.

## 0. Local tools

```bash
brew install age hashicorp/tap/terraform
gh auth login
```

## 1. Generate keys and secrets

Run these somewhere outside the repo (e.g. `~/amrgp-keys/`). Nothing here is ever committed.

```bash
# Backup encryption. The PRIVATE key decrypts every backup: store it in the club's
# password manager and somewhere offline. Losing it makes all backups useless.
age-keygen -o amrgp-backup-key.txt          # prints "Public key: age1..."

# SSH keys GitHub Actions uses to deploy (one per environment, no passphrase)
ssh-keygen -t ed25519 -N "" -C amrgp-deploy-prod    -f amrgp-deploy-prod
ssh-keygen -t ed25519 -N "" -C amrgp-deploy-sandbox -f amrgp-deploy-sandbox

# Per environment (run twice, keep prod and sandbox values separate)
openssl rand -hex 32       # POSTGRES_PASSWORD
openssl rand -base64 48    # JWT_SECRET
```

You also need your own SSH public key (`~/.ssh/id_ed25519.pub`) for admin access.

## 2. Cloudflare

1. **Zone ID:** dashboard → `alaskamountainrunners.org` → Overview → copy the Zone ID into `cloudflare_zone_id` in both `terraform/environments/*/terraform.tfvars`.
2. **API token:** the token Terraform uses needs, scoped to `alaskamountainrunners.org` only:
   - Zone → DNS → Edit
   - Zone → SSL and Certificates → Edit (to issue the origin certificate)
   - Zone → Zone → Read

   If your existing token lacks one, edit it or create a new one (My Profile → API Tokens).
3. **SSL mode:** SSL/TLS → Overview. If it's **Full** or **Full (strict)**, nothing to do. If it's **Flexible**, don't change the zone-wide setting (it would affect the club's WordPress site). Instead add Rules → Configuration Rules → a rule matching hostname `results.alaskamountainrunners.org` or `results-sandbox.alaskamountainrunners.org` that sets SSL to **Full (strict)**. Under Flexible, Cloudflare would talk to the server over plain HTTP and loop on Caddy's HTTPS redirect.
4. **Existing records:** DNS → Records: confirm nothing already exists named `results` or `results-sandbox` (Terraform would fail to create them).
5. **R2 (Terraform state):** R2 → enable it (free tier) → create bucket `amrgp-terraform-state`. Then R2 → Manage API tokens → create a token with **Object Read & Write** on that bucket only. Note the **Access Key ID**, **Secret Access Key**, and your **Account ID** (shown on the R2 overview page).
6. **Turnstile:** Turnstile → the widget whose site key is in `AmrGrandPrix.Client/.env` → add both hostnames above to its hostname list. Note the widget's **Secret Key**.

## 3. Vultr

1. Create an account and add a payment method.
2. Account → API → enable the API and copy the key.
3. On the same page, **Access Control** must allow GitHub Actions to call the API: add `0.0.0.0/0` (and `::/0`). The deploy workflow uses the API from GitHub's runners, whose IPs change constantly, to open SSH temporarily.

## 4. Backblaze B2

1. Create an account.
2. Buckets → Create a Bucket: name `amrgp-backups` (bucket names are global; if it's taken, use another and change `backup_bucket` in `terraform/environments/prod/terraform.tfvars` and the bucket name in `.github/workflows/backup-check.yml`), **Private**, default encryption on, **Object Lock enabled**. Then Bucket Settings → Object Lock → default retention **Compliance**, **14 days**.
   - Why: the server's B2 key can't delete files, but it can *hide* them, and the lifecycle rules below delete hidden files a day later. Object Lock stops anything (lifecycle rules, Terraform's key, the club account itself) from deleting a backup before it is 14 days old, so a compromised server can't wipe recent backups. Hidden files stay recoverable during that window.
   - Object Lock can't be turned off once enabled, and Compliance retention can't be shortened, even by the account owner. Keep it at 14 days (matching the `daily/` hide window); don't raise it "to be safe".
3. That bucket → Lifecycle Settings → **Use custom lifecycle rules**:

   | File name prefix | Days till hide | Days till delete |
   |---|---|---|
   | `daily/` | 14 | 1 |
   | `monthly/` | 183 | 1 |

4. Application Keys → **Add a New Application Key**: name `terraform`, access to **all buckets**, type Read and Write. Terraform uses it only to create the two narrow bucket keys (server writer, monitor reader). Note the **keyID** and **applicationKey**.

## 5. Resend

1. Create an account.
2. Domains → Add Domain → `results.alaskamountainrunners.org` (region us-east-1 is fine).
3. Leave **Receiving** off (the app only sends). Use Resend's automatic DNS setup for Cloudflare to add the domain's records, then click Verify.
   - **Skip any DMARC record it offers.** Grand Prix mail is covered by the club's existing `_dmarc.alaskamountainrunners.org` record (receivers fall back to it when the subdomain has none), and its reports go to Brevo with the rest of the club's mail.
   - These records aren't in Terraform (`email_dns_records = []`), so destroying or recreating the server doesn't touch them. If the club's DNS is ever rebuilt, rerun this setup.
4. API Keys → create two keys with **Sending access**, restricted to that domain: `amrgp-prod` and `amrgp-sandbox`.

## 6. Fill in the tfvars

Replace every `TODO` in `terraform/environments/prod/terraform.tfvars` and `terraform/environments/sandbox/terraform.tfvars`: zone ID, your admin public key, the matching deploy public key (`amrgp-deploy-*.pub`), and the age public key (`age1...`, prod only). Commit them; they contain no secrets.

## 7. GitHub

Repo: Settings → Secrets and variables → Actions. With `gh` (run from the repo root):

```bash
# Repository secrets (Terraform and the deploy firewall step)
gh secret set VULTR_API_KEY
gh secret set CLOUDFLARE_API_TOKEN
gh secret set R2_ACCESS_KEY_ID
gh secret set R2_SECRET_ACCESS_KEY
gh secret set B2_APPLICATION_KEY_ID
gh secret set B2_APPLICATION_KEY

# Repository variables
gh variable set CLOUDFLARE_ACCOUNT_ID --body "<account id>"
# Optional permanent admin SSH from your IP (otherwise add a rule in the Vultr dashboard when needed)
gh variable set ADMIN_SSH_CIDRS --body '["<your ip>/32"]'
```

Create the environments (Settings → Environments → New environment):

| Environment | Protection | Variables | Secrets |
|---|---|---|---|
| `sandbox` | none | `SITE_HOST=results-sandbox.alaskamountainrunners.org`, `CLUB_NOTIFICATION_EMAIL=<your address>` | `SSH_PRIVATE_KEY` (amrgp-deploy-sandbox), `POSTGRES_PASSWORD`, `JWT_SECRET`, `ANTHROPIC_API_KEY`, `TURNSTILE_SECRET_KEY`, `RESEND_API_KEY` (sandbox key) |
| `production` | none | `SITE_HOST=results.alaskamountainrunners.org`, `CLUB_NOTIFICATION_EMAIL=akmtnrunners@gmail.com`, `POSTGRES_DATA=/mnt/pgdata/data`, and after step 10: `SERVER_IP`, `FIREWALL_GROUP_ID` | same names, prod values (amrgp-deploy-prod key, prod Resend key) |
| `sandbox-infra` | none | | |
| `prod-infra` | **Required reviewers: you** | | |

Environment secrets with `gh`: `gh secret set SSH_PRIVATE_KEY --env sandbox < amrgp-deploy-sandbox`, `gh variable set SITE_HOST --env sandbox --body ...`.

## 8. Merge the deployment code

`workflow_dispatch` workflows only appear in the Actions tab once they're on `main`, so merge the deployment branch. Nothing deploys yet: `deploy-app.yml` waits for `PROD_DEPLOY_ENABLED`.

## 9. Sandbox

1. Actions → **Sandbox up** → Run workflow (pick the branch to deploy). It creates the server, builds the image, and deploys. The first run takes ~10 minutes, mostly cloud-init installing Docker.
2. Check, per [DEPLOYMENT_PLAN.md § Verification](../DEPLOYMENT_PLAN.md#verification):
   - `https://results-sandbox.alaskamountainrunners.org` loads with a valid certificate, including a deep link such as `/standings`
   - The origin refuses direct connections: `curl -m 5 http://<server ip>` times out
   - Registering sends a confirmation email that lands in a Gmail inbox (not spam); "Show original" shows SPF, DKIM, and DMARC `PASS`
   - A result report reaches `CLUB_NOTIFICATION_EMAIL`, not the club inbox
3. Make yourself an admin (see [First admin](#first-admin)) and try a results upload.
4. Actions → **Sandbox down** when done. It stops billing; a stopped (not destroyed) server is still charged.

## 10. Production

1. Actions → **Infrastructure** → environment `prod`, action `apply`. Read the plan job's log, then approve the `prod-infra` deployment.
2. From the apply log's Outputs, set the `production` environment variables `SERVER_IP` and `FIREWALL_GROUP_ID`.
3. Backup monitor key, for `backup-check.yml`, from your machine:

   ```bash
   cd terraform/environments/prod
   export AWS_ACCESS_KEY_ID=<r2 key id> AWS_SECRET_ACCESS_KEY=<r2 secret> \
          AWS_ENDPOINT_URL_S3=https://<account id>.r2.cloudflarestorage.com
   terraform init
   terraform output -raw backup_monitor_key_id | gh secret set BACKUP_MONITOR_KEY_ID
   terraform output -raw backup_monitor_key    | gh secret set BACKUP_MONITOR_KEY
   ```

4. `gh variable set PROD_DEPLOY_ENABLED --body true`, then Actions → **Deploy app** → Run workflow. From now on every push to `main` that touches the app deploys automatically.
5. Check DMARC: `dig +short TXT _dmarc.alaskamountainrunners.org` returns exactly one `v=DMARC1` record, and `dig +short TXT _dmarc.results.alaskamountainrunners.org` returns nothing.
6. Optional: replace the empty production database with your local one (see [Copy the local database](#copy-the-local-database)). Do it now, before anyone registers, because the restore replaces every table.
7. Make yourself an admin (below). Skip this if you restored a local database where your account is already an Admin.

## 11. Backup drill

SSH in as root (needs `ADMIN_SSH_CIDRS` or a temporary port-22 rule in the Vultr firewall group), then:

```bash
systemctl start amrgp-backup && journalctl -u amrgp-backup --no-pager | tail
```

After a few days, confirm the scheduled **Backup check** workflow is passing.

### Restore a backup locally

Do this at least once (and again after Postgres upgrades or key changes): it proves the backups are readable and that the age key in the password manager decrypts them.

**Requirements**

- The age private key (`amrgp-backup-key.txt`) from the club's password manager.
- `brew install age` (plus `rclone` if downloading from the command line).
- A Postgres **16+** client *and* server. Prod dumps are written by Postgres 16 (archive header 1.15); older clients fail with `pg_restore: error: unsupported version (1.15) in file header`, and restoring into an older server isn't supported. Homebrew's `postgresql@16` is keg-only, so put it on your PATH and make sure it is the server actually running:

  ```bash
  export PATH="/opt/homebrew/opt/postgresql@16/bin:$PATH"   # add to ~/.zshrc to keep it
  brew services list | grep postgres                       # stop any older postgresql@NN service
  brew services start postgresql@16
  psql -d postgres -c 'show server_version'                # 16.x
  ```

  A fresh 16 server has its own empty data directory and only a superuser role named after your macOS user. If the API's connection string used another role, point it at yours (no password needed for local connections):

  ```bash
  cd AmrGrandPrix.API
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=amr_grand_prix;Username=$(whoami)"
  ```

**1. Download the newest backup.** Easiest: B2 web console → Buckets → `amrgp-backups` → `daily/` → download the newest `db-<date>.dump.age`. From the command line, the server key is write-only and the monitor key list-only, so create a temporary **read-only** application key for the bucket (App Keys → Add) and delete it afterwards:

```bash
mkdir -p ~/amrgp-restore && cd ~/amrgp-restore
export RCLONE_CONFIG_B2_TYPE=b2 RCLONE_CONFIG_B2_ACCOUNT=<keyID> RCLONE_CONFIG_B2_KEY=<appKey>
rclone ls b2:amrgp-backups/daily/
rclone copy b2:amrgp-backups/daily/db-<date>.dump.age .
```

If you used the browser, `mkdir -p ~/amrgp-restore` and move the file there.

**2. Keep your local data** (skip if the local database doesn't exist yet or you don't need it):

```bash
pg_dump --format=custom -d amr_grand_prix -f ~/amrgp-restore/local-before-restore.dump
```

**3. Decrypt and sanity-check:**

```bash
cd ~/amrgp-restore
age --decrypt -i /path/to/amrgp-backup-key.txt db-<date>.dump.age > prod.dump
pg_restore --list prod.dump | head      # a table of contents, not an error
```

**4. Restore.** Stop the API first. Restore into your dev database to run the app against it, or into a scratch database (`amrgp_restore_test`) to leave your dev data alone. `--no-owner --no-privileges` is needed because the dump references prod's `amrgp` role:

```bash
dropdb --if-exists amr_grand_prix
createdb amr_grand_prix
pg_restore --exit-on-error --no-owner --no-privileges -d amr_grand_prix prod.dump
```

**5. Check it:**

```bash
psql amr_grand_prix -c 'select count(*) from "RaceResults"'
cd AmrGrandPrix.API && dotnet ef migrations list   # none "(Pending)"; if your branch adds migrations, dotnet ef database update
```

Then run the API and client and compare a few pages (standings, a runner, a race) with the live site. Logins are now the production accounts; local email still goes to MailHog.

**6. Clean up.** The decrypted dump holds password hashes and runners' personal details:

```bash
rm ~/amrgp-restore/prod.dump ~/amrgp-restore/db-*.dump.age
```

Delete the temporary B2 key if you made one. To get your old dev data back: `dropdb amr_grand_prix && createdb amr_grand_prix && pg_restore --no-owner -d amr_grand_prix ~/amrgp-restore/local-before-restore.dump`.

## Copy the local database

Moves everything in your dev database (series, races, results, runners, standings, and user accounts) to production, replacing what the first deploy created there. The restore drops and recreates the production database, so do it only while production holds nothing you need. The same steps work for the sandbox.

**1. Match the schema.** The dump carries its own migration history, and later deploys apply only the migrations it doesn't list. Your local database must therefore be at exactly the migrations of the commit you deployed: check out that commit (normally `main`) and run

```bash
cd AmrGrandPrix.API
dotnet ef migrations list     # every migration should be applied, none "(Pending)"
dotnet ef database update     # if any are pending
```

If your local database has migrations from an unmerged branch, merge them and deploy first, or roll the local database back with `dotnet ef database update <LastMigrationOnMain>`.

**2. Dump it locally.** Add `-h`/`-U` to match your local connection string if needed:

```bash
pg_dump --format=custom --no-owner --no-privileges -d amr_grand_prix -f amrgp-local.dump
```

Production runs Postgres 16, and its `pg_restore` can't read dumps made by a newer `pg_dump` (`pg_dump --version` 17+). If yours is newer, dump with a 16 client instead, e.g. `docker run --rm -v "$PWD:/out" postgres:16-alpine pg_dump --format=custom --no-owner --no-privileges -h host.docker.internal -U <user> -d amr_grand_prix -f /out/amrgp-local.dump`.

**3. Copy it to the server.** SSH needs `ADMIN_SSH_CIDRS` or a temporary port-22 rule in the Vultr firewall group.

```bash
scp amrgp-local.dump root@<server ip>:/opt/amrgp/
```

**4. Restore it on the server.** The app is stopped first so nothing writes while the database is replaced:

```bash
ssh root@<server ip>
cd /opt/amrgp
dc="docker compose -f docker-compose.prod.yml"
$dc stop app
$dc exec -T db sh -c 'dropdb -U "$POSTGRES_USER" "$POSTGRES_DB" && createdb -U "$POSTGRES_USER" "$POSTGRES_DB"'
$dc exec -T db sh -c 'pg_restore --exit-on-error --no-owner --no-privileges -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < amrgp-local.dump
$dc start app
rm amrgp-local.dump
```

**5. Check it.** The site loads with your data, and:

```bash
$dc exec db psql -U amrgp -d amr_grand_prix -c 'select count(*) from "RaceResults"'
systemctl start amrgp-backup    # take a backup of the restored data right away
```

Your local user accounts come along with their passwords and roles, so you can log in with your dev credentials. Delete any test accounts from the app's user management page. Delete `amrgp-local.dump` from your machine too: it contains password hashes and runners' personal details.

## Changing the hostname

How the site moved from `grandprix.` to `results.alaskamountainrunners.org`; the same steps apply to any future rename. The old hostname keeps working as a redirect.

**Before merging the rename:**

1. **Resend:** Domains → Add Domain → the new sending domain, automatic Cloudflare DNS setup (skip DMARC, as in [step 5](#5-resend)), then Verify. The API keys are restricted to the old domain, so create new **Sending access** keys restricted to the new one (`amrgp-prod`, `amrgp-sandbox`) and update `RESEND_API_KEY` in both GitHub environments. Revoke the old keys after the move. Merging deploys the new `Email:FromEmail`, and Resend rejects mail from an unverified domain.
2. **Turnstile:** add the new hostnames to the widget's hostname list. Keep the old ones until the move is done.
3. **SSL rule:** if the zone is on Flexible and you added a Configuration Rule for the Grand Prix hostnames ([step 2](#2-cloudflare)), add the new hostnames to it.
4. **DNS:** confirm no record already exists for the new names.

**Cutover** (the site is unreachable for a few minutes between steps 6 and 8):

5. Merge. The app redeploys with the new from address while still on the old hostname.
6. Actions → **Infrastructure** → `prod` → `apply`. Check the plan: it creates the new DNS record and replaces the origin certificate, and the old record only moves to `legacy` (no destroy). The server itself isn't touched (`user_data` is ignored).
7. Copy the new origin certificate to the server, because cloud-init installed the old one only at first boot. You'll need SSH access (`ADMIN_SSH_CIDRS` or a temporary port-22 rule) and the R2 state credentials. GitHub can't show the `R2_*` secrets back, so use the keys you saved in [step 2](#2-cloudflare). If you don't have them, create a temporary R2 API token with **Object Read & Write** on `amrgp-terraform-state` and delete it afterward. Without these variables, Terraform falls back to looking for AWS credentials and fails with `No valid credential sources found`. `terraform output` only reads state, so the Cloudflare and Vultr tokens aren't needed.

   ```bash
   cd terraform/environments/prod
   read -s "AWS_ACCESS_KEY_ID?R2 access key id: "; echo          # read -s keeps keys out of shell history (zsh syntax)
   read -s "AWS_SECRET_ACCESS_KEY?R2 secret access key: "; echo
   export AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY
   export AWS_ENDPOINT_URL_S3=https://<account id>.r2.cloudflarestorage.com
   terraform init -reconfigure                                    # -reconfigure in case of an earlier init -backend=false
   terraform output -raw origin_certificate | head -1             # should print -----BEGIN CERTIFICATE-----

   terraform output -raw origin_certificate | ssh root@<server ip> 'cat > /opt/amrgp/certs/origin.pem'
   terraform output -raw origin_private_key | ssh root@<server ip> 'umask 077; cat > /opt/amrgp/certs/origin.key'
   ```

8. `gh variable set SITE_HOST --env production --body <new hostname>`, then Actions → **Deploy app** → Run workflow. That points Caddy at the new name, restarts it with the new certificate, and smoke-tests `https://<new hostname>/health`.

**After:**

9. **Redirect:** Rules → Redirect Rules → Create (Single Redirect). Match: hostname equals the old hostname. Action: dynamic, expression `concat("https://<new hostname>", http.request.uri.path)`, status 301, **Preserve query string** on (confirmation and reset links carry their tokens in the query string).
10. Check it: the new hostname loads with a valid certificate; `https://<old hostname>/standings?year=2026` redirects to the same path and query on the new one; a password reset email arrives from the new address with SPF/DKIM/DMARC `PASS` and a link to the new hostname.
11. Everyone is signed out once: login tokens are stored per site, so they don't carry over to the new hostname.
12. Later, once Resend's dashboard shows no more mail from it, remove the old sending domain from Resend and delete its DNS records (`resend._domainkey.<old>`, `send.<old>`). Remove the old hostnames from Turnstile.

The sandbox has no legacy hostname. Run **Sandbox down** before applying a rename to it, and set its `SITE_HOST` before the next **Sandbox up**.

## Google Analytics

GA4 loads only in production builds, and only once the measurement ID is set. Until then the site works the same, just without analytics.

The results site reports to the club's existing GA4 property (the one for alaskamountainrunners.org), through **its own web data stream**. The WordPress site keeps its stream (MonsterInsights), so each site has its own measurement ID and stream settings, while the property shows both. Both sites set the same `_ga` cookie on `alaskamountainrunners.org`, so a visitor who uses both counts as one user in the property. Sessions are counted per stream, so a visit that crosses sites shows up as one session on each.

1. Signed in as the shared Alaska Mountain Runners Google account: Admin → Data streams → the results site's web stream (name it e.g. "Results site", URL `https://results.alaskamountainrunners.org`). Copy its measurement ID (`G-…`). Don't reuse the WordPress stream's ID: step 3 would then change the WordPress site's tracking.
2. **Property-wide settings:** Google signals (Admin → Data collection) and ads personalization (Admin → Data collection → Data collection for ads personalization) apply to every stream in the property, including WordPress. The results site turns both off in its own tag regardless (`lib/analytics.js`), which is what the `/privacy` page promises, so the property toggles are the club's choice for the WordPress site. The same goes for data retention. Add maintainers as users rather than making a personal account the owner.
3. In the results stream's **Enhanced measurement** settings, turn off **Page changes based on browser history events**. The app sends its own page views (`RouteTracker`), so leaving it on would count each page twice. This setting is per stream and doesn't affect WordPress.
4. `gh variable set GA_MEASUREMENT_ID --body "G-…"` (a repository variable: it's public in the page anyway). The next app deploy to production bakes it into the build; the sandbox never gets it.
5. After the deploy, check GA's **Realtime** or **DebugView** report.

**Reading the reports:** property reports combine both sites. To see one site, add a comparison or filter on **Stream name** (or **Hostname**: `results.alaskamountainrunners.org` vs. `alaskamountainrunners.org`). Custom events (`standings_view_change`, `athlete_search`, `result_claim`, `sign_up`, `landing_cta_click`) come only from the results site.

## First admin

Registration always grants the `ReadOnly` role. Register and confirm your account on the site, then promote it on the server:

```bash
ssh root@<server ip>
cd /opt/amrgp && docker compose -f docker-compose.prod.yml exec db \
  psql -U amrgp -d amr_grand_prix -c "
    INSERT INTO \"UserRoles\" (\"UserId\", \"RoleId\")
    SELECT u.\"Id\", r.\"Id\" FROM \"Users\" u, \"Roles\" r
    WHERE u.\"NormalizedEmail\" = upper('you@example.com') AND r.\"Name\" = 'Admin';"
```

Log out and back in to get a token with the new role. Further admins can be promoted from the app's user management page.
