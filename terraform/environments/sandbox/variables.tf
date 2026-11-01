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
  type    = list(string)
  default = []
}
