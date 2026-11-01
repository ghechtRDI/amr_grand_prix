terraform {
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
