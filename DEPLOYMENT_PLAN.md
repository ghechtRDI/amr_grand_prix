# Deploy AMR Grand Prix to `grandprix.alaskamountainrunners.org`

## Context

The app (React/Vite client + .NET 9 API + Postgres) has never been deployed — there's a `Dockerfile.production` and a `docker-compose.prod.yml` in the repo suggesting a prior deployment attempt, but they're stale and currently non-functional (details below). The goal now is a real, low-maintenance deployment for a low-traffic, highly-seasonal site (~10k monthly visits at peak, much less in winter) that:

- Serves securely at `grandprix.alaskamountainrunners.org` (DNS already on Cloudflare)
- Is provisioned via Terraform and deployed via CI/CD (GitHub Actions — repo is `github.com/ghechtRDI/amr_grand_prix`)
- Can have a sandbox environment stood up on demand and torn down when not needed (cost ≈ $0 when off)
- Backs up the Postgres database offsite, independent of the primary host, since this becomes the system of record for race results
- Sends the app's email (account confirmation, password reset, result-report notifications to the club) reliably, without landing in spam

Chosen approach: a single **Vultr** Cloud Compute VPS in **Seattle** running Docker Compose, Cloudflare in front for TLS/CDN/WAF, **Resend** for outbound email, offsite backups to Backblaze B2. Steady-state cost ≈ **$11-13/month**; sandbox ≈ $5/month only while it's up, $0 when destroyed. This choice is provisional pending discussion with the AMR board — see [Alternatives considered](#alternatives-considered).

## Status (2026-10-31)

All the code is written; what remains is account setup and the first applies, in [DOCS/DEPLOYMENT_SETUP.md](DOCS/DEPLOYMENT_SETUP.md). The AMR board approved Vultr.

## Prerequisite fixes — done

Verified by running `docker-compose.prod.yml` locally: the SPA, client-side routes, API, and 404s are served through Caddy; migrations apply; register → confirm email → login → authenticated request works; and behind a proxy, email links come out as `https://<real host>`.

1. **SPA served.** `Program.cs` has `UseDefaultFiles`/`UseStaticFiles` and `MapFallbackToFile("index.html")`, with unknown `/api/*` routes kept as 404 rather than returning the SPA.
2. **Postgres in `docker-compose.prod.yml`.** Rewritten as Caddy → app → `postgres:16-alpine`; settings come from `.env` (template: `.env.example`). The compose project is named `amr-grand-prix-prod` so prod commands can't touch the dev MailHog/Ollama containers.
3. **Reverse proxy.** `UseForwardedHeaders` (X-Forwarded-For/Proto) runs first. `UseHttpsRedirection` was removed rather than kept: the container has no HTTPS listener, and Cloudflare/Caddy redirect before requests reach it. Any proxy is trusted because the app's port is only reachable from the compose network. Caddy sets X-Forwarded-For from `CF-Connecting-IP`, so the rate limiter and Turnstile see the real client IP.
4. **CORS** reads `Cors:AllowedOrigins` (policy renamed `AppPolicy`). Production is same-origin, so it only matters for the Vite dev/preview servers.
5. **Secrets out of git.** `.env.production` removed; `.gitignore` ignores `.env`/`.env.*` except `.env.example` and `AmrGrandPrix.Client/.env`.
6. **No HTTPS listener in the container** (port 5001 gone).
7. **Email.** `FromEmail` is `noreply@grandprix.alaskamountainrunners.org`; the report recipient is `Email:ClubNotificationEmail`.
8. **`Dockerfile.production`.** `npm ci` includes devDependencies; `Dockerfile.production.dockerignore` keeps host `node_modules`/`bin`/`obj`, `.git`, `ResultsArchive`, and `.env*` out of the build; `curl` is installed for the HEALTHCHECK; and an EF Core **migrations bundle** (`efbundle`) is built into the image, since the runtime image has no SDK to run `dotnet ef database update`.

Also added along the way:
- Production refuses to start with the placeholder `Jwt:Secret` from `appsettings.json`.
- The race catalog is seeded outside Development only when the database has no series, so a fresh production database gets it but admin renames/merges aren't undone.

## Infrastructure design

### Terraform layout — environments as directories, not workspaces

```
terraform/
  modules/
    vps_app/                  # one Vultr instance + firewall + DNS + origin cert (+ optional block storage)
      main.tf                 # vultr_instance, vultr_firewall_group/_rule, vultr_ssh_key, vultr_block_storage
      dns.tf                  # cloudflare_dns_record (proxied A record), cloudflare_origin_ca_certificate
      email_dns.tf            # Resend DKIM/SPF/return-path records + DMARC
      cloud-init.tftpl        # templated user-data
      files/amrgp-backup.sh   # nightly backup script installed by cloud-init
    b2_backup/
      main.tf                 # bucket-scoped b2_application_keys (the bucket itself is created by hand)
  environments/
    prod/                     # main.tf, variables.tf, backend.tf (R2), terraform.tfvars
    sandbox/                  # same layout; separate state key, so it can't touch prod
```

Directories-per-environment (not `terraform workspace select`) because sandbox is destroyed/recreated frequently and should never share state with prod.

Key resources (`vultr/vultr` provider):

- `vultr_instance` — region `sea` (Seattle, the closest Vultr region to Alaska). Prod: Regular Cloud Compute `vc2-1c-2gb` (1 vCPU / 2GB / 55GB SSD, $10/mo) with Vultr automatic backups enabled (+20%, ~$2/mo) as a second line of defense behind B2. Sandbox: `vc2-1c-1gb` ($5/mo), no automatic backups. Cloud-init adds a 2GB swap file on both — .NET + Postgres + Caddy fit in 2GB for this traffic, but swap absorbs spikes (and the one-off migration container).
- `vultr_firewall_group` + `vultr_firewall_rule` — 80/443 using the rule's built-in `source = "cloudflare"` option so only Cloudflare's edge can reach the origin (no hand-maintained IP list). SSH is closed apart from optional `ADMIN_SSH_CIDRS`; since GitHub's runners have no fixed IPs, each deploy adds a port-22 rule for its runner through the Vultr API and removes it when the job ends.
- `vultr_block_storage` (prod only, 40GB HDD minimum, `prevent_destroy`) for the Postgres data directory, mounted at `/mnt/pgdata`. Block storage is offered in `sea` (checked against the Vultr API).
- `cloudflare_dns_record` (proxied, orange-cloud) + `cloudflare_origin_ca_certificate` consumed by Caddy for the edge-to-origin leg. The sandbox is `grandprix-sandbox.alaskamountainrunners.org`, not `sandbox.grandprix…`: Cloudflare's free Universal SSL certificate only covers one subdomain level.
- The instance ignores `user_data` changes, since cloud-init only runs on first boot; replace the server deliberately (`terraform apply -replace`) to pick up cloud-init changes.

Terraform state: **Cloudflare R2** (S3-compatible backend) rather than Terraform Cloud — avoids a second auth surface since Cloudflare's already in play for DNS, effectively free for this state size.

### Provisioning vs. deploy — the line

- **Cloud-init (Terraform, once per server):** install Docker, create swap, create the `deploy` user, write the origin certificate, mount the block storage, install the backup script and its systemd timer. It doesn't start the app: the stack can't run without the `.env` that only the deploy has.
- **GitHub Actions (every deploy):** build the image, push to GHCR, `scp` `docker-compose.prod.yml`, the Caddyfile, and a `.env` rendered from GitHub Secrets, start Postgres, run the EF migrations bundle as a one-off container (before the app starts, since it seeds roles on startup), `up -d --wait`, then smoke-test `/health` through Cloudflare.

### GitHub Actions workflows

```
.github/workflows/
  pr-checks.yml         # pull_request: dotnet test, npm lint/build, terraform fmt/validate
  deploy-app.yml        # push to main (app paths), once PROD_DEPLOY_ENABLED: test -> build -> deploy production
  infra-plan-apply.yml  # workflow_dispatch(environment, action), plus a prod plan on PRs touching terraform/
  sandbox-up.yml        # workflow_dispatch: terraform apply sandbox -> build -> deploy
  sandbox-down.yml      # workflow_dispatch: terraform destroy sandbox
  backup-check.yml      # daily: fail if the newest B2 backup is over 36 hours old
  _build.yml / _deploy.yml / _terraform.yml   # reusable pieces the above call

Apply/destroy runs after a separate plan job, in a `<env>-infra` GitHub Environment; `prod-infra` requires a reviewer.
```

### Email — Resend

The API sends mail over SMTP through `EmailService` (`System.Net.Mail.SmtpClient`): account confirmation and password reset (`AuthController`) and result-report notifications to the club (`ReportsController`). Volume is a few dozen messages a month.

We don't run our own mail server: self-hosted mail from a fresh VPS IP lands in spam, and Vultr blocks outbound port 25 by default anyway. Instead the app relays through **Resend** (free tier: 3,000 emails/month, 100/day — far above our volume).

- **Sending domain:** `grandprix.alaskamountainrunners.org` (from address `noreply@grandprix.alaskamountainrunners.org`, from name "Alaska Mountain Runners Grand Prix"). Using the subdomain keeps the app's sending reputation separate from the club's main domain and doesn't touch whatever mail setup `alaskamountainrunners.org` already has.
- **One-time setup (manual, like the B2 bucket):** create the Resend account under a club-owned address, add the domain in Resend, and create one API key per environment (prod, sandbox) restricted to sending.
- **DNS (Terraform, `email_dns.tf`):** the DKIM TXT record, SPF TXT + MX records on Resend's return-path subdomain, exactly as Resend's domain page lists them, plus `_dmarc.grandprix` TXT starting at `v=DMARC1; p=none; rua=mailto:<club address>` (tighten to `quarantine` once reports look clean). These are DNS-only (grey-cloud) records.
- **App config (env vars from GitHub Secrets):** `Email__SmtpHost=smtp.resend.com`, `Email__SmtpPort=587`, `Email__EnableSsl=true`, `Email__SmtpUsername=resend`, `Email__SmtpPassword=<Resend API key>`, `Email__FromEmail=noreply@grandprix.alaskamountainrunners.org`. Port **587 (STARTTLS)** is required: `System.Net.Mail.SmtpClient` doesn't support implicit TLS, so port 465 won't work.
- **Sandbox:** uses its own Resend API key, the same verified domain, and `Email__ClubNotificationEmail` pointed at a developer address so test reports never reach the club inbox.
- MailHog stays dev-only — it is not part of `docker-compose.prod.yml`.

### Backups — Backblaze B2

Nightly systemd timer on the VPS (not GitHub Actions, which has no reachability into the private DB) runs `amrgp-backup`: `pg_dump -Fc` (already compressed, so no gzip) encrypted with `age` to an offline-held key, uploaded to `daily/` and, on the 1st of the month, also `monthly/`. The bucket and its lifecycle rules (`daily/` hidden after 14 days, `monthly/` after 183) are created once by hand; Terraform only manages two bucket-scoped keys. The bucket has Object Lock (Compliance, 14-day default retention): the server's key can't delete files, but it can hide them and the lifecycle rules would then delete them, so the lock is what keeps a compromised server (or a leaked club key) from erasing recent backups. `backup-check.yml` lists the bucket with a list-only key and fails if the newest backup is stale, with no SSH needed. Vultr's automatic instance backups are a convenience for whole-box restores, not a substitute — they live with the same provider.

### Cost estimate

- **Prod steady-state:** Vultr `vc2-1c-2gb` $10 + automatic backups ~$2 + block storage ~$1, Resend $0 (free tier), B2 storage ~$0.05-0.30, R2 state ~$0, Cloudflare free plan ~$0 → **~$11-13/month**
- **Sandbox:** ~$5/month while up, **$0 when destroyed** (must fully `terraform destroy`, not just stop — Vultr bills stopped instances)

## Alternatives considered

Hetzner Cloud was the original choice (~$7/month), but its cheap CX line is only sold in Germany/Finland; the US locations (Ashburn, Hillsboro) offer only CPX shared (~$23+/month for 4GB) and CCX dedicated (~$51+/month) plans, which erases the price advantage.

| Option | Approx. monthly | Notes |
|---|---|---|
| **Vultr, Seattle (chosen)** | $11-13 | Cheapest; closest region to Alaska; only port 25 blocked; good Terraform provider. We maintain the server (OS updates, Docker, Caddy). |
| Sevalla (Kinsta's app/DB platform) | ~$16, up to ~$45 | Same vendor/billing/login as the club's Kinsta WordPress site (Kinsta moved app + DB hosting to Sevalla in Feb 2026). Managed PaaS: no server to patch, daily DB backups, official Terraform provider. App pod S1 (0.5 CPU/1GB) $10 + Postgres 0.25 CPU/256MB/1GB $5; the next DB tier is $34. Best fit if the board prefers nobody maintaining a server. Confirm a US-West region is available. |
| DigitalOcean, San Francisco | ~$14 | Best docs, but SMTP ports 25/465/587 are permanently blocked on droplets — email would need Resend's alternate port 2587 or its HTTP API. |
| AWS Lightsail, Oregon | ~$14 | 2 vCPU/2GB, pairs with Amazon SES; thinner Terraform coverage, burstable CPU. |
| Linode (Akamai), Seattle | ~$14 | Equivalent to Vultr, but mail ports blocked until a support ticket is approved. |
| Hetzner US (CPX) | ~$23+ | Closest to the original design, no longer cheap. |

## Verification

1. `dotnet build` / `dotnet test` and `npm run lint` / `npm run build` still pass after the Program.cs / compose / email-config fixes.
2. Locally: `docker compose -f docker-compose.prod.yml up --build`, confirm the SPA loads at `http://localhost` (not just the API), `/health` returns 200, and login/JWT flow works against the local Postgres container.
3. Infrastructure workflow, `sandbox` / `plan` — review the plan before ever applying against real Vultr/Cloudflare accounts.
4. After first real `terraform apply` (sandbox first, not prod): confirm `grandprix-sandbox.alaskamountainrunners.org` resolves through Cloudflare, TLS is valid (Cloudflare Full/Strict mode via the origin cert), the origin refuses direct (non-Cloudflare) connections on 80/443, and `sandbox-up.yml` succeeds end-to-end (build → migrate → deploy → smoke test).
5. Email (after the prod apply creates the Resend DNS records): Resend shows the domain as verified; registering a new account on the sandbox delivers a confirmation email to a Gmail inbox (not spam), and "Show original" in Gmail reports SPF, DKIM, and DMARC all `PASS`. Submit a result report on the sandbox and confirm it reaches the developer notification address, not the club inbox.
6. Manually trigger the backup script once on prod (`systemctl start amrgp-backup`), confirm the encrypted dump lands in the B2 bucket and can be decrypted/restored to a scratch Postgres instance.
7. Only then open production to users. The step-by-step order is in [DOCS/DEPLOYMENT_SETUP.md](DOCS/DEPLOYMENT_SETUP.md).
