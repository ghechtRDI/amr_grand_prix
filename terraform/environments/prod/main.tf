# Production: grandprix.alaskamountainrunners.org
# Provider credentials come from the environment: VULTR_API_KEY, CLOUDFLARE_API_TOKEN,
# B2_APPLICATION_KEY_ID / B2_APPLICATION_KEY.
terraform {
  required_version = ">= 1.10"

  required_providers {
    vultr = {
      source  = "vultr/vultr"
      version = "~> 2.33"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.28"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.4"
    }
    b2 = {
      source  = "Backblaze/b2"
      version = "~> 0.14"
    }
  }
}

provider "vultr" {}
provider "cloudflare" {}
provider "b2" {}

module "backup" {
  source = "../../modules/b2_backup"

  name        = "prod"
  bucket_name = var.backup_bucket
}

module "app" {
  source = "../../modules/vps_app"

  name               = "prod"
  hostname           = var.hostname
  cloudflare_zone_id = var.cloudflare_zone_id
  plan               = "vc2-1c-2gb" # 1 vCPU / 2GB / 55GB, $10/mo
  automatic_backups  = true         # +$2/mo; second line of defense behind B2
  data_volume_gb     = 40           # HDD block storage minimum, ~$1/mo

  admin_ssh_public_key  = var.admin_ssh_public_key
  deploy_ssh_public_key = var.deploy_ssh_public_key
  admin_ssh_cidrs       = var.admin_ssh_cidrs

  backup = {
    bucket          = var.backup_bucket
    key_id          = module.backup.writer_key_id
    application_key = module.backup.writer_key
    age_recipient   = var.backup_age_recipient
  }

  email_dns_records = var.email_dns_records
  dmarc_rua         = var.dmarc_rua
}

output "server_ipv4" {
  value = module.app.server_ipv4
}

output "firewall_group_id" {
  value = module.app.firewall_group_id
}

output "backup_monitor_key_id" {
  value = module.backup.monitor_key_id
}

output "backup_monitor_key" {
  value     = module.backup.monitor_key
  sensitive = true
}
