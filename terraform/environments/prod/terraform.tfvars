# Non-secret production settings. Fill in the TODOs during setup (DOCS/DEPLOYMENT_SETUP.md).
hostname           = "grandprix.alaskamountainrunners.org"
cloudflare_zone_id = "9630584fe8d7f2d85de8965c539ef74f"

admin_ssh_public_key  = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHa5OMkWckCqLrUnCXq4CErUdObCTbZV5Fn6SHL4dmv5 galen.hecht995@gmail.com" # your ~/.ssh/id_ed25519.pub
deploy_ssh_public_key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBaJD7Gnr8OJRj1fSHhggUMJRY4seeYH2Tvq6Fq2KvkO amrgp-deploy-prod"        # amrgp-deploy-prod.pub (private half is the production SSH_PRIVATE_KEY secret)

backup_bucket        = "amrgp-backups"
backup_age_recipient = "age1sufr0nvygrk728q9luaunfxc8c07ts7a32q2pjq3f50ud5eww3tq78k0sf" # age1... public key; the private key stays offline

# Empty: Grand Prix mail falls back to the club's existing _dmarc.alaskamountainrunners.org
# record (reports go to Brevo). Set an address only to give the subdomain its own policy.
dmarc_rua = ""

# Resend's records (DKIM, return path) are added to Cloudflare by Resend's automatic
# DNS setup, not Terraform (DOCS/DEPLOYMENT_SETUP.md step 5). To manage them here
# instead, list them with fully qualified names, e.g.
#   { name = "send.grandprix.alaskamountainrunners.org", type = "CNAME", content = "..." },
# and remove the copies Resend created first.
email_dns_records = []
