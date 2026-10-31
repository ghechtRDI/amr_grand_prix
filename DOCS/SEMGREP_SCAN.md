# Static Analysis with Semgrep

How to run a [Semgrep Community Edition](https://semgrep.dev/docs/getting-started/quickstart-ce) (free) static analysis scan of the API, client, and Docker config. Run it before each deployment and after any significant change to auth, controllers, or Docker files.

## 1. Install

```bash
brew install semgrep
semgrep --version
```

No account or `semgrep login` is needed. Logging in enables the paid Pro engine; everything below uses the free Community rules.

## 2. Ignore file

The repo root has a `.semgrepignore` that excludes build output, dependencies, generated EF migrations, and the results archive:

```
node_modules/
**/bin/
**/obj/
**/dist/
AmrGrandPrix.API/Migrations/
ResultsArchive/
```

A `.semgrepignore` replaces Semgrep's built-in defaults instead of adding to them, so keep `node_modules/`, `bin/`, and `obj/` in it.

## 3. Run the scan

From the repo root:

```bash
mkdir -p semgrep-reports

semgrep scan \
  --no-git-ignore \
  --config p/csharp \
  --config p/javascript \
  --config p/react \
  --config p/secrets \
  --config p/owasp-top-ten \
  --config p/dockerfile \
  --config p/docker-compose \
  --metrics=off \
  --text --output semgrep-reports/semgrep-report.txt \
  --json-output semgrep-reports/semgrep-report.json
```

The flags that matter:

- **`--no-git-ignore`**: by default Semgrep scans only files tracked by git, so new files that haven't been committed yet are skipped. This flag scans them too; `.semgrepignore` still keeps `node_modules` and build output out.
- **`--metrics=off`**: stops Semgrep sending usage data. Picking each ruleset by name works without metrics; `--config auto` doesn't.
- **`--output` / `--json-output`**: write a readable report and a machine-readable report. `semgrep-reports/` is gitignored.

The scan takes about a minute. The rulesets:

| Ruleset | Covers |
|---|---|
| `p/csharp` | .NET API: SQL injection through raw EF queries, insecure deserialization, weak crypto, JWT validation settings |
| `p/javascript`, `p/react` | Client: XSS (`dangerouslySetInnerHTML`), `eval`, unsafe URLs |
| `p/secrets` | Hardcoded API keys, connection strings, tokens |
| `p/owasp-top-ten` | Broad sweep across all languages |
| `p/dockerfile`, `p/docker-compose` | Containers running as root, privileged containers, insecure compose settings |

For a quicker scan with Semgrep's general-purpose ruleset, replace all the `--config` flags with `--config p/default`.

## 4. Review findings

The scan summary prints the number of findings. Open `semgrep-reports/semgrep-report.txt` to see each one with its rule ID, file and line, explanation, and a `sg.run` link to the rule documentation.

Severity is shown by the arrows next to each finding:

- `❯❯❱` **ERROR**: fix or deliberately accept before deploying.
- `❯❱` **WARNING**: review; usually worth fixing.
- `❱` **INFO**: advisory.

To list findings compactly from the JSON report:

```bash
python3 -c "
import json
for r in json.load(open('semgrep-reports/semgrep-report.json'))['results']:
    print(r['extra']['severity'], r['check_id'].split('.')[-1], f\"{r['path']}:{r['start']['line']}\")"
```

### Suppressing a false positive

If a finding is reviewed and judged safe, suppress it on that line with the rule ID and a reason so the next reviewer knows why:

```csharp
ValidateLifetime = false, // nosemgrep: jwt-tokenvalidationparameters-no-expiry-validation -- <why this is safe>
```

```dockerfile
# nosemgrep: dockerfile.security.missing-user.missing-user -- dev-only image, prod uses Dockerfile.production
CMD ["npm", "run", "dev"]
```

Don't suppress a finding without a reason.

## 5. What Semgrep doesn't cover

Semgrep Community Edition analyzes code one function or file at a time and only knows the patterns in its rules. Pair it with:

- **Vulnerable dependencies:** Semgrep doesn't check package versions.
  ```bash
  cd AmrGrandPrix.API && dotnet list package --vulnerable --include-transitive
  cd AmrGrandPrix.Client && npm audit --omit=dev
  ```
- **Cross-file data flow:** untrusted input passed from a controller into a service in another file isn't traced. That requires the paid Pro engine or a manual review.
- **Authorization logic:** a missing or wrong `[Authorize]` / role on an endpoint isn't detected. Review controller attributes by hand, or run `/security-review` in Claude Code.
- **Secrets outside known formats:** `p/secrets` matches recognizable key patterns only. `.env` files and `appsettings*.json` still need a manual check that no real credentials are committed.
