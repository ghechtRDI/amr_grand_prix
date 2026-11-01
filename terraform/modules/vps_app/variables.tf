variable "name" {
  description = "Short environment name used in labels (e.g. prod, sandbox)"
  type        = string
}

variable "hostname" {
  description = "Public hostname served through Cloudflare (e.g. grandprix.alaskamountainrunners.org)"
  type        = string
}

variable "cloudflare_zone_id" {
  description = "Zone ID of alaskamountainrunners.org"
  type        = string
}

variable "region" {
  description = "Vultr region; sea (Seattle) is the closest to Alaska"
  type        = string
  default     = "sea"
}

variable "plan" {
  description = "Vultr plan ID (vc2-1c-2gb for prod, vc2-1c-1gb for sandbox)"
  type        = string
}

variable "os_id" {
  description = "Vultr OS ID; 2284 is Ubuntu 24.04 LTS x64"
  type        = number
  default     = 2284
}

variable "automatic_backups" {
  description = "Enable Vultr's daily whole-instance backups (+20% of the plan price)"
  type        = bool
  default     = false
}

variable "data_volume_gb" {
  description = "Size of a block storage volume for Postgres data, mounted at /mnt/pgdata. 0 keeps data on the instance disk."
  type        = number
  default     = 0
}

variable "admin_ssh_public_key" {
  description = "SSH public key installed for root, for manual administration"
  type        = string
}

variable "deploy_ssh_public_key" {
  description = "SSH public key for the 'deploy' user that GitHub Actions connects as"
  type        = string
}

variable "admin_ssh_cidrs" {
  description = "IPv4 CIDRs allowed to reach SSH permanently (e.g. [\"203.0.113.4/32\"]). Deploys open a temporary rule for the runner instead."
  type        = list(string)
  default     = []
}

variable "backup" {
  description = "Nightly encrypted pg_dump to Backblaze B2. Null disables it."
  type = object({
    bucket          = string
    key_id          = string
    application_key = string
    age_recipient   = string
  })
  default   = null
  sensitive = true
}

variable "email_dns_records" {
  description = "DNS records Resend lists for the sending domain (DKIM TXT, return-path MX and SPF TXT). Names are fully qualified."
  type = list(object({
    name     = string
    type     = string
    content  = string
    priority = optional(number)
  }))
  default = []
}

variable "dmarc_rua" {
  description = "Address for DMARC aggregate reports; empty skips the DMARC record"
  type        = string
  default     = ""
}
