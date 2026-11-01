# Non-secret production settings. Fill in the TODOs during setup (DOCS/DEPLOYMENT_SETUP.md).
hostname           = "grandprix.alaskamountainrunners.org"
cloudflare_zone_id = 9630584fe8d7f2d85de8965c539ef74f

admin_ssh_public_key  = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHa5OMkWckCqLrUnCXq4CErUdObCTbZV5Fn6SHL4dmv5 galen.hecht995@gmail.com" # your ~/.ssh/id_ed25519.pub
deploy_ssh_public_key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIBaJD7Gnr8OJRj1fSHhggUMJRY4seeYH2Tvq6Fq2KvkO amrgp-deploy-prod" # amrgp-deploy-prod.pub (private half is the production SSH_PRIVATE_KEY secret)

backup_bucket        = "amrgp-backups"
backup_age_recipient = "age1sufr0nvygrk728q9luaunfxc8c07ts7a32q2pjq3f50ud5eww3tq78k0sf" # age1... public key; the private key stays offline

dmarc_rua = "akmtnrunners@gmail.com"

# Copy from Resend > Domains > grandprix.alaskamountainrunners.org once the domain is added.
# Names must be fully qualified. Typically:
#   { name = "resend._domainkey.grandprix.alaskamountainrunners.org", type = "TXT", content = "p=..." },
#   { name = "send.grandprix.alaskamountainrunners.org", type = "MX", content = "feedback-smtp.us-east-1.amazonses.com", priority = 10 },
#   { name = "send.grandprix.alaskamountainrunners.org", type = "TXT", content = "v=spf1 include:amazonses.com ~all" },
email_dns_records = [
   { name = "resend._domainkey.grandprix", type = "TXT", content = "p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDYaflqQX8oyKGK40g5r+xl/IskRog/8oUWntzaOGKn6Ef9E+Fh2erF67OFvMtSvVeRk1k6qKNU9ciNKsLrtqeM2212CiI4vGiiDNJelUyhImdeXsVAO0lZZ5EvPAxs2eIwGUgeBiu61637wz6RWvuqYHdxkwXl9wBY7apU2pwatwIDAQAB" },
   { name = "grandprix", type = "MX", content = "freceive.forge.rmta.net", priority = 10 },
   { name = "rsend.grandprix", type = "CNAME", content = "rsend.forge.rmta.net" },
   { name = "send.grandprix", type = "CNAME", content = "send.forge.rmta.net" },
]
