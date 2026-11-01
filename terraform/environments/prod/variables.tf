variable "hostname" {
  type = string
}

variable "cloudflare_zone_id" {
  type = string
}

variable "admin_ssh_public_key" {
  type = string
}

variable "deploy_ssh_public_key" {
  type = string
}

variable "admin_ssh_cidrs" {
  description = "Optional permanent SSH access; usually set via TF_VAR_admin_ssh_cidrs rather than committed"
  type        = list(string)
  default     = []
}

variable "backup_bucket" {
  type = string
}

variable "backup_age_recipient" {
  description = "age public key (age1...) that backups are encrypted to; keep the private key offline"
  type        = string
}

variable "email_dns_records" {
  type = list(object({
    name     = string
    type     = string
    content  = string
    priority = optional(number)
  }))
  default = []
}

variable "dmarc_rua" {
  type    = string
  default = ""
}
