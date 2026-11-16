output "server_ipv4" {
  value = vultr_instance.app.main_ip
}

output "firewall_group_id" {
  description = "Deploys add and remove a temporary SSH rule in this group"
  value       = vultr_firewall_group.app.id
}

output "hostname" {
  value = var.hostname
}

# cloud-init installs the origin certificate only on first boot, so a replacement (e.g.
# after a hostname change) has to be copied to /opt/amrgp/certs by hand; see
# DOCS/DEPLOYMENT_SETUP.md "Changing the hostname".
output "origin_certificate" {
  value     = cloudflare_origin_ca_certificate.app.certificate
  sensitive = true
}

output "origin_private_key" {
  value     = tls_private_key.origin.private_key_pem
  sensitive = true
}
