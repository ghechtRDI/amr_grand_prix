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
3. **SSL mode:** SSL/TLS → Overview. If it's **Full** or **Full (strict)**, nothing to do. If it's **Flexible**, don't change the zone-wide setting (it would affect the club's WordPress site). Instead add Rules → Configuration Rules → a rule matching hostname `grandprix.alaskamountainrunners.org` or `grandprix-sandbox.alaskamountainrunners.org` that sets SSL to **Full (strict)**. Under Flexible, Cloudflare would talk to the server over plain HTTP and loop on Caddy's HTTPS redirect.
4. **Existing records:** DNS → Records: confirm nothing already exists named `grandprix` or `grandprix-sandbox` (Terraform would fail to create them).
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
2. Domains → Add Domain → `grandprix.alaskamountainrunners.org` (region us-east-1 is fine).
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
| `sandbox` | none | `SITE_HOST=grandprix-sandbox.alaskamountainrunners.org`, `CLUB_NOTIFICATION_EMAIL=<your address>` | `SSH_PRIVATE_KEY` (amrgp-deploy-sandbox), `POSTGRES_PASSWORD`, `JWT_SECRET`, `ANTHROPIC_API_KEY`, `TURNSTILE_SECRET_KEY`, `RESEND_API_KEY` (sandbox key) |
| `production` | none | `SITE_HOST=grandprix.alaskamountainrunners.org`, `CLUB_NOTIFICATION_EMAIL=akmtnrunners@gmail.com`, `POSTGRES_DATA=/mnt/pgdata/data`, and after step 10: `SERVER_IP`, `FIREWALL_GROUP_ID` | same names, prod values (amrgp-deploy-prod key, prod Resend key) |
| `sandbox-infra` | none | | |
| `prod-infra` | **Required reviewers: you** | | |

Environment secrets with `gh`: `gh secret set SSH_PRIVATE_KEY --env sandbox < amrgp-deploy-sandbox`, `gh variable set SITE_HOST --env sandbox --body ...`.

## 8. Merge the deployment code

`workflow_dispatch` workflows only appear in the Actions tab once they're on `main`, so merge the deployment branch. Nothing deploys yet: `deploy-app.yml` waits for `PROD_DEPLOY_ENABLED`.

## 9. Sandbox

1. Actions → **Sandbox up** → Run workflow (pick the branch to deploy). It creates the server, builds the image, and deploys. The first run takes ~10 minutes, mostly cloud-init installing Docker.
2. Check, per [DEPLOYMENT_PLAN.md § Verification](../DEPLOYMENT_PLAN.md#verification):
   - `https://grandprix-sandbox.alaskamountainrunners.org` loads with a valid certificate, including a deep link such as `/standings`
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
5. Check DMARC: `dig +short TXT _dmarc.alaskamountainrunners.org` returns exactly one `v=DMARC1` record, and `dig +short TXT _dmarc.grandprix.alaskamountainrunners.org` returns nothing.
6. Make yourself an admin (below).

## 11. Backup drill

SSH in as root (needs `ADMIN_SSH_CIDRS` or a temporary port-22 rule in the Vultr firewall group), then:

```bash
systemctl start amrgp-backup && journalctl -u amrgp-backup --no-pager | tail
```

Download `daily/db-<date>.dump.age` from the B2 bucket and restore it into a scratch database locally:

```bash
createdb amrgp_restore_test
age --decrypt -i amrgp-backup-key.txt db-<date>.dump.age | pg_restore --no-owner -d amrgp_restore_test
psql amrgp_restore_test -c 'select count(*) from "RaceResults"'
```

After a few days, confirm the scheduled **Backup check** workflow is passing.

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
