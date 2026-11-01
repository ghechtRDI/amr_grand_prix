# State lives in a Cloudflare R2 bucket (S3-compatible). Each environment has its own
# key, so sandbox runs can never touch prod state. Credentials come from the environment:
#   AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY  (an R2 API token's S3 credentials)
#   AWS_ENDPOINT_URL_S3=https://<cloudflare account id>.r2.cloudflarestorage.com
terraform {
  backend "s3" {
    bucket = "amrgp-terraform-state"
    key    = "sandbox/terraform.tfstate"
    region = "auto"

    use_lockfile               = true
    use_path_style             = true
    skip_credentials_validation = true
    skip_region_validation     = true
    skip_requesting_account_id = true
    skip_metadata_api_check    = true
    skip_s3_checksum           = true
  }
}
