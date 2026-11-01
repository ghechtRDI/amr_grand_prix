# Sandbox: grandprix-sandbox.alaskamountainrunners.org, created and destroyed on demand
# (sandbox-up.yml / sandbox-down.yml). No backups or block storage: its data is disposable.
# A first-level subdomain on purpose: Cloudflare's free Universal SSL certificate only
# covers *.alaskamountainrunners.org, not sandbox.grandprix.alaskamountainrunners.org.
# Provider credentials come from the environment: VULTR_API_KEY, CLOUDFLARE_API_TOKEN.
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
  }
}

provider "vultr" {}
provider "cloudflare" {}

module "app" {
  source = "../../modules/vps_app"

  name               = "sandbox"
  hostname           = var.hostname
  cloudflare_zone_id = var.cloudflare_zone_id
  plan               = "vc2-1c-1gb" # $5/mo while it exists

  admin_ssh_public_key  = var.admin_ssh_public_key
  deploy_ssh_public_key = var.deploy_ssh_public_key
  admin_ssh_cidrs       = var.admin_ssh_cidrs
}

output "server_ipv4" {
  value = module.app.server_ipv4
}

output "firewall_group_id" {
  value = module.app.firewall_group_id
}
