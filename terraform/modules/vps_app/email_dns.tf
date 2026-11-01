# Optional Resend sending-domain records. Normally Resend's automatic DNS setup adds
# them and email_dns_records is empty; if listed, copy them exactly from Resend's
# domain page with fully qualified names. DNS-only (not proxied).
resource "cloudflare_dns_record" "email" {
  for_each = { for r in var.email_dns_records : "${r.type} ${r.name}" => r }

  zone_id  = var.cloudflare_zone_id
  name     = each.value.name
  type     = each.value.type
  content  = each.value.content
  priority = each.value.priority
  proxied  = false
  ttl      = 1
  comment  = "Resend (Terraform)"
}

# Start at p=none to collect reports; tighten to quarantine once they look clean
resource "cloudflare_dns_record" "dmarc" {
  count = var.dmarc_rua != "" ? 1 : 0

  zone_id = var.cloudflare_zone_id
  name    = "_dmarc.${var.hostname}"
  type    = "TXT"
  content = "\"v=DMARC1; p=none; rua=mailto:${var.dmarc_rua}\""
  proxied = false
  ttl     = 1
  comment = "DMARC for Grand Prix email (Terraform)"
}
