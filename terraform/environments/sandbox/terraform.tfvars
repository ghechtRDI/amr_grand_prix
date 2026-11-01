# Non-secret sandbox settings. Fill in the TODOs during setup (DOCS/DEPLOYMENT_SETUP.md).
hostname           = "grandprix-sandbox.alaskamountainrunners.org"
cloudflare_zone_id = "9630584fe8d7f2d85de8965c539ef74f"

admin_ssh_public_key  = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHa5OMkWckCqLrUnCXq4CErUdObCTbZV5Fn6SHL4dmv5 galen.hecht995@gmail.com" # your ~/.ssh/id_ed25519.pub
deploy_ssh_public_key = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIPxJLbOGGzgIJ5vB7tdsnCYxFU5O2kHqVCh/uzWfckcK amrgp-deploy-sandbox"     # amrgp-deploy-sandbox.pub (private half is the sandbox SSH_PRIVATE_KEY secret)
