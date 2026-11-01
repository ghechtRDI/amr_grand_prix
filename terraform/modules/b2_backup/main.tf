# Application keys for the backup bucket. The bucket and its lifecycle rules are
# created once by hand (see DOCS/DEPLOYMENT_SETUP.md) so Terraform can never delete it.
terraform {
  required_providers {
    b2 = {
      source  = "Backblaze/b2"
      version = "~> 0.14"
    }
  }
}

variable "name" {
  description = "Environment name used in key names"
  type        = string
}

variable "bucket_name" {
  type = string
}

data "b2_bucket" "backups" {
  bucket_name = var.bucket_name
}

# Used by the server's nightly backup. No deleteFiles, but writeFiles can still
# hide files, so the bucket's Object Lock is what protects recent backups.
resource "b2_application_key" "writer" {
  key_name     = "amrgp-${var.name}-backup-writer"
  bucket_ids   = [data.b2_bucket.backups.bucket_id]
  capabilities = ["listBuckets", "listFiles", "writeFiles"]
}

# Used by the backup-check workflow to confirm a recent backup exists
resource "b2_application_key" "monitor" {
  key_name     = "amrgp-${var.name}-backup-monitor"
  bucket_ids   = [data.b2_bucket.backups.bucket_id]
  capabilities = ["listBuckets", "listFiles"]
}

output "writer_key_id" {
  value = b2_application_key.writer.application_key_id
}

output "writer_key" {
  value     = b2_application_key.writer.application_key
  sensitive = true
}

output "monitor_key_id" {
  value = b2_application_key.monitor.application_key_id
}

output "monitor_key" {
  value     = b2_application_key.monitor.application_key
  sensitive = true
}
