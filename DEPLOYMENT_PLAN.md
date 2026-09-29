# Deploy AMR Grand Prix to `grandprix.alaskamountainrunners.org`

## Context

The app (React/Vite client + .NET 9 API + Postgres) has never been deployed — there's a `Dockerfile.production` and a `docker-compose.prod.yml` in the repo suggesting a prior deployment attempt, but they're stale and currently non-functional (details below). The goal now is a real, low-maintenance deployment for a low-traffic, highly-seasonal site (~10k monthly visits at peak, much less in winter) that:

- Serves securely at `grandprix.alaskamountainrunners.org` (DNS already on Cloudflare)
- Is provisioned via Terraform and deployed via CI/CD (GitHub Actions — repo is `github.com/ghechtRDI/amr_grand_prix`)
- Can have a sandbox environment stood up on demand and torn down when not needed (cost ≈ $0 when off)
- Backs up the Postgres database offsite, independent of the primary host, since this becomes the system of record for race results

Chosen approach (decided with the user): a single Hetzner Cloud VPS running Docker Compose, Cloudflare in front for TLS/CDN/WAF, offsite backups to Backblaze B2. This is the cheapest of the options considered (DIY VPS vs. DigitalOcean App Platform vs. Fly.io vs. AWS App Runner+RDS) and fits the app's low, spiky traffic profile — steady-state cost ≈ **$7-8/month**, sandbox ≈ $7/month only while it's up, $0 when destroyed.

## Prerequisite fixes (must land before any deployment works)

These block a working deployment regardless of host and were found during exploration:

1. **SPA isn't actually served.** `Dockerfile.production` copies the Vite build into `wwwroot`, but `AmrGrandPrix.API/Program.cs` registers no static-file or SPA-fallback middleware. Add `app.UseDefaultFiles()`, `app.UseStaticFiles()`, and `app.MapFallbackToFile("index.html")` (after `MapControllers()`, so API routes still win).
2. **`docker-compose.prod.yml` provisions the wrong database.** It runs `mcr.microsoft.com/mssql/server:2022-latest` with a `SA_PASSWORD`, but `Program.cs:29` uses `options.UseNpgsql(...)` — SQL Server was never going to work. Replace the `db` service with `postgres:16-alpine` (matching the dev `docker-compose.yml`), fix the connection string to Npgsql format, and point the volume at Postgres's data dir.
3. **Reverse-proxy HTTPS redirect loop.** `Program.cs:188-189` calls `app.UseHttpsRedirection()` when not in Development. Once Caddy terminates TLS and forwards plain HTTP to the app container (the design below), the app will see every request as HTTP and issue a redirect for each one. Add `ForwardedHeadersOptions` (`app.UseForwardedHeaders(...)` with `ForwardedHeaders.XForwardedFor | XForwardedProto`, trusting Caddy's internal IP) before `UseHttpsRedirection`, and configure Caddy to send `X-Forwarded-Proto`.
4. **CORS is hardcoded to localhost.** `Program.cs:151-162`'s `DevPolicy` only allows `localhost` origins. Since the client is served same-origin from the API in production, CORS mostly matters for the sandbox's separate subdomain — make allowed origins configurable via `Cors:AllowedOrigins` in config (env-overridable), including `https://grandprix.alaskamountainrunners.org` and the sandbox subdomain.
5. **Secrets committed to git.** `.env`, `.env.development`, and `.env.production` are tracked in git (not gitignored) and contain plaintext DB passwords. `git rm --cached` them, add `.env*` (except `.env.example` templates) to `.gitignore`, and replace with `.env.*.example` placeholder files. Real values move to GitHub Actions secrets, injected at deploy time (see below).
6. Also drop the container's own HTTPS listener (`ASPNETCORE_URLS=https://+:5001` / port 5001 in `docker-compose.prod.yml`) — Caddy handles TLS, so the app only needs to listen on plain HTTP internally.

## Infrastructure design

### Terraform layout — environments as directories, not workspaces

```
terraform/
  modules/
    vps_app/                  # one Hetzner server + Caddy + DNS + firewall
      main.tf                 # hcloud_server, hcloud_firewall, hcloud_ssh_key
      dns.tf                  # cloudflare_record (proxied A record), cloudflare_origin_ca_certificate
      variables.tf            # server_type, location, subdomain
      outputs.tf              # server_ipv4
      cloud-init.tftpl         # templated user-data
    b2_backup/
      main.tf                 # b2_application_key (bucket itself created once, manually or via B2 CLI)
  environments/
    prod/
      main.tf                 # module "app" { source = "../../modules/vps_app" ... }
      backend.tf               # Cloudflare R2, S3-compatible backend
      volume.tf                # hcloud_volume + attachment for Postgres data (survives server recreate)
      terraform.tfvars
    sandbox/
      main.tf                  # same module, smallest server tier, subdomain "sandbox.grandprix"
      backend.tf                # separate state key — can't touch prod even with -auto-approve
      sandbox.tfvars
```

Directories-per-environment (not `terraform workspace select`) because sandbox is destroyed/recreated frequently and should never share state with prod.

Key resources: `hcloud_server` (Hillsboro "hil" region for US-West proximity, CX22 2vCPU/4GB for prod, smallest tier for sandbox), `hcloud_firewall` (22/tcp from your admin IP only, 80/443 from Cloudflare's published IP ranges only), `hcloud_volume` for the Postgres data directory, `cloudflare_record` (proxied, orange-cloud), `cloudflare_origin_ca_certificate` consumed by Caddy for the edge-to-origin leg.

Terraform state: **Cloudflare R2** (S3-compatible backend) rather than Terraform Cloud — avoids a second auth surface since Cloudflare's already in play for DNS, effectively free for this state size.

### Provisioning vs. deploy — the line

- **Cloud-init (Terraform, once per server):** install Docker, write the committed `docker-compose.prod.yml` and Caddyfile, do one `docker compose up -d` so a fresh box is self-sufficient, install a systemd timer for the backup script.
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

### Backups — Backblaze B2

Nightly systemd timer on the VPS (not GitHub Actions, which has no reachability into the private DB): `pg_dump -Fc | gzip | age -r <pubkey> | rclone rcat b2:amrgp-backups/db-$(date +%F).sql.gz.age`. B2 bucket + lifecycle rule (14 daily + 6 monthly) created once via B2's own console/CLI; Terraform only manages the rotatable application key. A separate scheduled `backup-check.yml` workflow SSHes in (or checks a small status endpoint) to alert if the last backup is stale.

### Cost estimate

- **Prod steady-state:** Hetzner CX22 ~$7, B2 storage ~$0.05-0.30, R2 state ~$0, Cloudflare free plan ~$0 → **~$7-8/month**
- **Sandbox:** ~$7/month while up, **$0 when destroyed** (must fully `terraform destroy`, not just stop, to avoid lingering volume/IP charges)

## Verification

1. `dotnet build` / `dotnet test` and `npm run lint` / `npm run build` still pass after the Program.cs / compose fixes.
2. Locally: `docker compose -f docker-compose.prod.yml up --build`, confirm the SPA loads at `http://localhost` (not just the API), `/health` returns 200, and login/JWT flow works against the local Postgres container.
3. `terraform -chdir=terraform/environments/sandbox init && terraform plan -var-file=sandbox.tfvars` — review the plan before ever applying against real Hetzner/Cloudflare accounts.
4. After first real `terraform apply` (sandbox first, not prod): confirm `sandbox.grandprix.alaskamountainrunners.org` resolves through Cloudflare, TLS is valid (Cloudflare Full/Strict mode via the origin cert), and `deploy-app.yml` run against sandbox succeeds end-to-end (build → migrate → deploy → smoke test).
5. Manually trigger the backup script once, confirm the encrypted dump lands in the B2 bucket and can be decrypted/restored to a scratch Postgres instance.
6. Only then run `terraform apply` for `prod` and point `deploy-app.yml` at it.
