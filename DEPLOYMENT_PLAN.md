# Deploy AMR Grand Prix to `grandprix.alaskamountainrunners.org`

## Context

The app (React/Vite client + .NET 9 API + Postgres) has never been deployed — there's a `Dockerfile.production` and a `docker-compose.prod.yml` in the repo suggesting a prior deployment attempt, but they're stale and currently non-functional (details below). The goal now is a real, low-maintenance deployment for a low-traffic, highly-seasonal site (~10k monthly visits at peak, much less in winter) that:

- Serves securely at `grandprix.alaskamountainrunners.org` (DNS already on Cloudflare)
- Is provisioned via Terraform and deployed via CI/CD (GitHub Actions — repo is `github.com/ghechtRDI/amr_grand_prix`)
- Can have a sandbox environment stood up on demand and torn down when not needed (cost ≈ $0 when off)
- Backs up the Postgres database offsite, independent of the primary host, since this becomes the system of record for race results
- Sends the app's email (account confirmation, password reset, result-report notifications to the club) reliably, without landing in spam

Chosen approach: a single **Vultr** Cloud Compute VPS in **Seattle** running Docker Compose, Cloudflare in front for TLS/CDN/WAF, **Resend** for outbound email, offsite backups to Backblaze B2. Steady-state cost ≈ **$11-13/month**; sandbox ≈ $5/month only while it's up, $0 when destroyed. This choice is provisional pending discussion with the AMR board — see [Alternatives considered](#alternatives-considered).

## Prerequisite fixes (must land before any deployment works)

These block a working deployment regardless of host and were found during exploration:

1. **SPA isn't actually served.** `Dockerfile.production` copies the Vite build into `wwwroot`, but `AmrGrandPrix.API/Program.cs` registers no static-file or SPA-fallback middleware. Add `app.UseDefaultFiles()`, `app.UseStaticFiles()`, and `app.MapFallbackToFile("index.html")` (after `MapControllers()`, so API routes still win).
2. **`docker-compose.prod.yml` provisions the wrong database.** It runs `mcr.microsoft.com/mssql/server:2022-latest` with a `SA_PASSWORD`, but `Program.cs:29` uses `options.UseNpgsql(...)` — SQL Server was never going to work. Replace the `db` service with `postgres:16-alpine`, fix the connection string to Npgsql format, and point the volume at Postgres's data dir.
3. **Reverse-proxy HTTPS redirect loop.** `Program.cs:188-189` calls `app.UseHttpsRedirection()` when not in Development. Once Caddy terminates TLS and forwards plain HTTP to the app container (the design below), the app will see every request as HTTP and issue a redirect for each one. Add `ForwardedHeadersOptions` (`app.UseForwardedHeaders(...)` with `ForwardedHeaders.XForwardedFor | XForwardedProto`, trusting Caddy's internal IP) before `UseHttpsRedirection`, and configure Caddy to send `X-Forwarded-Proto`.
4. **CORS is hardcoded to localhost.** `Program.cs:151-162`'s `DevPolicy` only allows `localhost` origins. Since the client is served same-origin from the API in production, CORS mostly matters for the sandbox's separate subdomain — make allowed origins configurable via `Cors:AllowedOrigins` in config (env-overridable), including `https://grandprix.alaskamountainrunners.org` and the sandbox subdomain.
5. **Secrets committed to git.** `.env.production` is tracked in git (not gitignored) and contains a plaintext DB password (the unused `.env` and `.env.development` have been deleted, but remain in history — rotate anything real they held). `git rm --cached` it, add `.env*` to `.gitignore` with exceptions for `.env.example` templates and `AmrGrandPrix.Client/.env` (public Turnstile site key, needed by the Vite build), and replace it with a `.env.example` placeholder file. Real values move to GitHub Actions secrets, injected at deploy time (see below).
6. Also drop the container's own HTTPS listener (`ASPNETCORE_URLS=https://+:5001` / port 5001 in `docker-compose.prod.yml`) — Caddy handles TLS, so the app only needs to listen on plain HTTP internally.
7. **Email sender uses a domain we don't own.** `appsettings.json` sets `Email:FromEmail` to `noreply@amrgrandprix.com`; mail from it will fail SPF/DKIM and be rejected or junked. Change the production value to `noreply@grandprix.alaskamountainrunners.org` (see [Email](#email--resend)). Also move the hardcoded `ClubEmail` (`akmtnrunners@gmail.com`) in `ReportsController.cs:22` into config (e.g. `Email:ClubNotificationEmail`) so the sandbox can send report notifications somewhere other than the club inbox.
8. ~~**`Dockerfile.production` build fixes.**~~ Done: `npm ci` now installs devDependencies (`--only=production` skipped `vite`, so the client build failed); `Dockerfile.production.dockerignore` keeps host `node_modules`/`bin`/`obj` (macOS native binaries), `.git`, `ResultsArchive`, and `.env*` files out of the build context; and the runtime stage installs `curl`, which the `HEALTHCHECK` needs but the `aspnet` base image lacks. Verified with a local `docker build`. The unused dev `api`/`client`/`db` compose services, their Dockerfiles, `docker-dev.sh`, `docker-prod.sh`, and `DOCKER.md` were removed.

## Infrastructure design

### Terraform layout — environments as directories, not workspaces

```
terraform/
  modules/
    vps_app/                  # one Vultr instance + Caddy + DNS + firewall
      main.tf                 # vultr_instance, vultr_firewall_group/_rule, vultr_ssh_key
      dns.tf                  # cloudflare_record (proxied A record), cloudflare_origin_ca_certificate
      email_dns.tf            # cloudflare_record for Resend SPF/DKIM/return-path + DMARC
      variables.tf            # plan, region, subdomain
      outputs.tf              # server_ipv4
      cloud-init.tftpl        # templated user-data
    b2_backup/
      main.tf                 # b2_application_key (bucket itself created once, manually or via B2 CLI)
  environments/
    prod/
      main.tf                 # module "app" { source = "../../modules/vps_app" ... }
      backend.tf              # Cloudflare R2, S3-compatible backend
      volume.tf               # vultr_block_storage + attachment for Postgres data (survives instance recreate)
      terraform.tfvars
    sandbox/
      main.tf                 # same module, smallest plan, subdomain "sandbox.grandprix"
      backend.tf              # separate state key — can't touch prod even with -auto-approve
      sandbox.tfvars
```

Directories-per-environment (not `terraform workspace select`) because sandbox is destroyed/recreated frequently and should never share state with prod.

Key resources (`vultr/vultr` provider):

- `vultr_instance` — region `sea` (Seattle, the closest Vultr region to Alaska). Prod: Regular Cloud Compute `vc2-1c-2gb` (1 vCPU / 2GB / 55GB SSD, $10/mo) with Vultr automatic backups enabled (+20%, ~$2/mo) as a second line of defense behind B2. Sandbox: `vc2-1c-1gb` ($5/mo), no automatic backups. Cloud-init adds a 2GB swap file on both — .NET + Postgres + Caddy fit in 2GB for this traffic, but swap absorbs spikes (and the one-off migration container).
- `vultr_firewall_group` + `vultr_firewall_rule` — 22/tcp from your admin IP only; 80/443 using the rule's built-in `source = "cloudflare"` option so only Cloudflare's edge can reach the origin (no hand-maintained IP list).
- `vultr_block_storage` (prod only) for the Postgres data directory — confirm block storage is offered in `sea` before applying; if it isn't, keep data on the instance disk and lean on B2 + Vultr automatic backups.
- `cloudflare_record` (proxied, orange-cloud) + `cloudflare_origin_ca_certificate` consumed by Caddy for the edge-to-origin leg.

Terraform state: **Cloudflare R2** (S3-compatible backend) rather than Terraform Cloud — avoids a second auth surface since Cloudflare's already in play for DNS, effectively free for this state size.

### Provisioning vs. deploy — the line

- **Cloud-init (Terraform, once per server):** install Docker, create swap, write the committed `docker-compose.prod.yml` and Caddyfile, do one `docker compose up -d` so a fresh box is self-sufficient, install a systemd timer for the backup script.
- **GitHub Actions (every push to main):** build the image, push to GHCR, render `.env` from GitHub Secrets and `scp` it over, run `dotnet ef database update` as a one-off container, then `docker compose pull && up -d`, then smoke-test `/health`.

### GitHub Actions workflows

```
.github/workflows/
  pr-checks.yml         # pull_request: dotnet build/test, npm lint/build, terraform fmt/validate/plan
  deploy-app.yml        # push to main (app/Dockerfile paths): build+push GHCR -> migrate -> deploy -> smoke test
  infra-plan-apply.yml  # workflow_dispatch(environment, action) + PR plan-only; apply/destroy gated behind a GitHub Environment with required reviewer
  sandbox-up.yml        # workflow_dispatch: terraform apply -var-file=sandbox.tfvars, then reuse deploy job against sandbox host
  sandbox-down.yml      # workflow_dispatch: terraform destroy -var-file=sandbox.tfvars
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

Nightly systemd timer on the VPS (not GitHub Actions, which has no reachability into the private DB): `pg_dump -Fc | gzip | age -r <pubkey> | rclone rcat b2:amrgp-backups/db-$(date +%F).sql.gz.age`. B2 bucket + lifecycle rule (14 daily + 6 monthly) created once via B2's own console/CLI; Terraform only manages the rotatable application key. A separate scheduled `backup-check.yml` workflow SSHes in (or checks a small status endpoint) to alert if the last backup is stale. Vultr's automatic instance backups are a convenience for whole-box restores, not a substitute — they live with the same provider.

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
3. `terraform -chdir=terraform/environments/sandbox init && terraform plan -var-file=sandbox.tfvars` — review the plan before ever applying against real Vultr/Cloudflare accounts.
4. After first real `terraform apply` (sandbox first, not prod): confirm `sandbox.grandprix.alaskamountainrunners.org` resolves through Cloudflare, TLS is valid (Cloudflare Full/Strict mode via the origin cert), the origin refuses direct (non-Cloudflare) connections on 80/443, and `deploy-app.yml` run against sandbox succeeds end-to-end (build → migrate → deploy → smoke test).
5. Email: Resend shows the domain as verified; registering a new account on the sandbox delivers a confirmation email to a Gmail inbox (not spam), and "Show original" in Gmail reports SPF, DKIM, and DMARC all `PASS`. Submit a result report on the sandbox and confirm it reaches the developer notification address, not the club inbox.
6. Manually trigger the backup script once, confirm the encrypted dump lands in the B2 bucket and can be decrypted/restored to a scratch Postgres instance.
7. Only then run `terraform apply` for `prod` and point `deploy-app.yml` at it.
