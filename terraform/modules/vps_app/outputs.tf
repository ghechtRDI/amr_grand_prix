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
