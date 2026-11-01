resource "vultr_ssh_key" "admin" {
  name    = "amrgp-${var.name}-admin"
  ssh_key = var.admin_ssh_public_key
}

# Only Cloudflare's edge may reach 80/443, so the origin can't be hit directly.
# SSH is closed except for admin_ssh_cidrs; deploys add a temporary rule for the
# GitHub runner's IP and remove it afterwards (see .github/workflows/_deploy.yml).
resource "vultr_firewall_group" "app" {
  description = "amrgp-${var.name}"
}

locals {
  web_rules = {
    for pair in setproduct(["v4", "v6"], ["80", "443"]) : "${pair[0]}-${pair[1]}" => {
      ip_type = pair[0]
      port    = pair[1]
    }
  }
}

resource "vultr_firewall_rule" "web" {
  for_each = local.web_rules

  firewall_group_id = vultr_firewall_group.app.id
  protocol          = "tcp"
  ip_type           = each.value.ip_type
  # With source = "cloudflare", Vultr uses Cloudflare's published ranges and ignores subnet
  source      = "cloudflare"
  subnet      = each.value.ip_type == "v4" ? "0.0.0.0" : "::"
  subnet_size = 0
  port        = each.value.port
  notes       = "Cloudflare ${each.value.port}"
}

resource "vultr_firewall_rule" "admin_ssh" {
  for_each = toset(var.admin_ssh_cidrs)

  firewall_group_id = vultr_firewall_group.app.id
  protocol          = "tcp"
  ip_type           = "v4"
  subnet            = split("/", each.value)[0]
  subnet_size       = tonumber(split("/", each.value)[1])
  port              = "22"
  notes             = "admin ssh"
}

resource "vultr_instance" "app" {
  region            = var.region
  plan              = var.plan
  os_id             = var.os_id
  label             = "amrgp-${var.name}"
  hostname          = "amrgp-${var.name}"
  tags              = ["amrgp", var.name]
  enable_ipv6       = true
  firewall_group_id = vultr_firewall_group.app.id
  ssh_key_ids       = [vultr_ssh_key.admin.id]

  backups = var.automatic_backups ? "enabled" : "disabled"
  dynamic "backups_schedule" {
    for_each = var.automatic_backups ? [1] : []
    content {
      type = "daily"
      hour = 12 # UTC; ~3-4am in Alaska
    }
  }

  user_data = templatefile("${path.module}/cloud-init.tftpl", {
    deploy_ssh_public_key = var.deploy_ssh_public_key
    origin_cert           = cloudflare_origin_ca_certificate.app.certificate
    origin_key            = tls_private_key.origin.private_key_pem
    data_volume           = var.data_volume_gb > 0
    backup                = var.backup
    backup_script         = file("${path.module}/files/amrgp-backup.sh")
  })

  lifecycle {
    # cloud-init only runs on first boot, so a user_data change would rebuild the
    # server. Replace it deliberately instead (terraform apply -replace=...).
    ignore_changes = [user_data]
  }
}

resource "vultr_block_storage" "pgdata" {
  count = var.data_volume_gb > 0 ? 1 : 0

  region               = var.region
  size_gb              = var.data_volume_gb
  block_type           = "storage_opt"
  label                = "amrgp-${var.name}-pgdata"
  attached_to_instance = vultr_instance.app.id
  live                 = true

  lifecycle {
    # Holds the database; destroying it must be a deliberate, manual act
    prevent_destroy = true
  }
}
