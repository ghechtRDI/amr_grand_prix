resource "cloudflare_dns_record" "app" {
  zone_id = var.cloudflare_zone_id
  name    = var.hostname
  type    = "A"
  content = vultr_instance.app.main_ip
  proxied = true
  ttl     = 1 # automatic (required for proxied records)
  comment = "AMR Grand Prix ${var.name} (Terraform)"
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
