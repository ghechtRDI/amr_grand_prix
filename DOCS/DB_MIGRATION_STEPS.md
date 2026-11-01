Yes. Both ends are Postgres, so a standard `pg_dump` / `pg_restore` moves everything: races, results, runners, GP points and standings, finalized seasons, upload history and user accounts. It's a one-time step for after the production server is up and before people start entering data there.

**Steps:**

1. **Bring the local schema up to date** so it matches the migrations in the deployed image:
   ```bash
   cd AmrGrandPrix.API && dotnet ef database update
   ```
2. **Dump the local database** in custom format, without ownership or permissions. Your local role names won't exist on the server, so those would fail to restore:
   ```bash
   pg_dump -Fc --no-owner --no-privileges -d amr_grand_prix -f amrgp.dump
   ```
3. **Copy the dump to the server** over SSH. That's the only way in: the database isn't exposed, and the firewall only allows SSH from your IP.
   ```bash
   scp amrgp.dump <server>:~/
   ```
4. **On the server, stop the app and restore** into the Postgres container:
   ```bash
   docker compose -f docker-compose.prod.yml stop app
   docker compose -f docker-compose.prod.yml exec -T db \
     pg_restore --clean --if-exists --no-owner -U amrgp -d amr_grand_prix < amrgp.dump
   docker compose -f docker-compose.prod.yml start app
   ```
   `--clean --if-exists` replaces whatever the first deploy's migration run created. The migration history table comes across too, so later deploys only apply new migrations.

**Things to watch:**

- **Version difference:** your local Postgres is 14 and production is 16. Restoring a 14 dump into 16 is supported; the reverse direction isn't.
- **Test data comes along:** that includes test user accounts, test races and so on. Password hashes carry over fine, so your admin login will work in production. If you have throwaway test accounts or races, delete them locally first, or clean them up after the restore.
- **Try it on the sandbox first.** That matches the plan's sandbox-before-prod order and lets you check that standings, runner pages and login all look right.
- **Only the database moves.** The race catalog, uploaded results and their stored LLM output are all in it. Files outside the database, like the PDFs in `ResultsArchive/`, don't, but the app doesn't need them.
- **Delete `amrgp.dump` from the server afterwards.** It contains user emails and password hashes.
