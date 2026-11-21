# Named "site" rather than "app" so a hostname change can move the old record to "legacy"
# (a moved block can't leave its source address still declared).
resource "cloudflare_dns_record" "site" {
  zone_id = var.cloudflare_zone_id
  name    = var.hostname
  type    = "A"
  content = vultr_instance.app.main_ip
  proxied = true
  ttl     = 1 # automatic (required for proxied records)
  comment = "AMR Grand Prix ${var.name} (Terraform)"
}

# Former hostnames. Cloudflare only applies Redirect Rules to proxied hostnames, so each
# keeps a proxied record; the redirect itself (old host -> var.hostname, path and query
# kept) is a Single Redirect rule in the dashboard, because managing the zone's redirect
# ruleset here would take over the club's other redirect rules too. Requests never reach
# the origin, which has no certificate for these names.
resource "cloudflare_dns_record" "legacy" {
  for_each = toset(var.legacy_hostnames)

  zone_id = var.cloudflare_zone_id
  name    = each.value
  type    = "A"
  content = vultr_instance.app.main_ip
  proxied = true
  ttl     = 1
  comment = "AMR Grand Prix ${var.name} former hostname, redirected by a Cloudflare rule (Terraform)"
}

# Cloudflare origin certificate for the Cloudflare -> Caddy leg. Trusted only by
# Cloudflare, so the hostname's SSL mode must be Full (strict) or Full.
resource "tls_private_key" "origin" {
  algorithm   = "ECDSA"
  ecdsa_curve = "P256"
}

resource "tls_cert_request" "origin" {
  private_key_pem = tls_private_key.origin.private_key_pem
  subject {
    common_name = var.hostname
  }
}

resource "cloudflare_origin_ca_certificate" "app" {
  csr                = tls_cert_request.origin.cert_request_pem
  hostnames          = [var.hostname]
  request_type       = "origin-ecc"
  requested_validity = 5475 # 15 years, the maximum
}
