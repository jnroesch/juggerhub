# JuggerHub Infrastructure (feature 015)

Terraform-defined hosting on **Azure Kubernetes Service (AKS)**. One definition,
applied to many environments via **Terraform workspaces + per-env tfvars**
(`envs/dev.tfvars`, `envs/prod.tfvars`, later `staging`). Environments are
architecturally identical and differ only in sizing/config.

See the design docs in [`../specs/015-hosting/`](../specs/015-hosting/): `plan.md`,
`research.md`, `data-model.md`, `contracts/`, `quickstart.md`.

> **Status**: authored, **not yet applied**. Nothing here has been run against a live
> subscription. `terraform init/validate/plan` and the smoke checks are pending.

---

## Layout

```text
infra/
├── bootstrap/                     # run ONCE, OUTSIDE Terraform (manual)
│   ├── New-TfStateBackend.ps1         # state RG + storage account + container (FR-013)
│   └── New-GitHubOidcServicePrincipal.ps1  # CI service principal + OIDC federation
├── envs/                          # per-environment values only
│   ├── dev.tfvars
│   ├── prod.tfvars
│   └── staging.tfvars.example
├── modules/
│   ├── network/                   # RG, VNet, subnet, static public IP
│   ├── aks/                       # cluster + system/user node pools
│   ├── platform/                  # ingress-nginx + cert-manager + LE issuers (Helm)
│   └── app/                       # namespace, secrets, postgres, backend, frontend, ingress
├── backend.tf  providers.tf  versions.tf
├── variables.tf  locals.tf  main.tf  outputs.tf
└── .tflint.hcl
```

---

## One-time bootstrap (manual, before any Terraform)

You are creating the Azure **subscription manually** first. Then, from a shell with
`az login` (an account that can create RGs, app registrations, and assign subscription
roles):

1. **State backend** — the storage that holds Terraform state:
   ```powershell
   ./bootstrap/New-TfStateBackend.ps1 -SubscriptionId <sub-guid>
   ```
   Storage account names are globally unique; pass `-StorageAccountName` if the
   default is taken, and mirror it in `backend.tf` (or use `-backend-config`).

2. **CI service principal** — the identity GitHub Actions uses (OIDC, no secret):
   ```powershell
   ./bootstrap/New-GitHubOidcServicePrincipal.ps1 `
       -SubscriptionId <sub-guid> -RepoOwner <owner> -RepoName juggerhub `
       -IncludePullRequest
   ```
   It prints `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` — set
   these as **GitHub Actions variables** (repo or per-Environment). It assigns the SP
   **Contributor** + **User Access Administrator** on the subscription and **Storage
   Blob Data Contributor** on the state account. See [Why a service principal?](#why-a-service-principal).

3. **Grant yourself state access**: give your operator account **Storage Blob Data
   Contributor** on the state storage account (the backend uses keyless AAD auth).

4. **Populate GitHub Environments** `development` and `production` (the names
   deploy.yml already uses) with the secrets `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`,
   `RESEND_API_KEY`, `ADMIN_EMAILS`, and the variables `AZURE_CLIENT_ID`,
   `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (from step 2). `GHCR_PULL_TOKEN` is
   **optional** — only needed if the GHCR image packages are private; with public
   packages it can be omitted and no imagePullSecret is created. The deploy workflow
   maps the secrets to `TF_VAR_*`. See `../specs/015-hosting/data-model.md`.

---

## Provision an environment (operator, manual)

```powershell
cd infra
$env:ARM_SUBSCRIPTION_ID = "<sub-guid>"        # or set -var subscription_id
terraform init
terraform workspace new dev                    # first time (else: select)
terraform apply -var-file=envs/dev.tfvars -var="image_tag=<sha>"
```

Then point DNS (registrar-managed): read `terraform output -raw ingress_public_ip` and
create an **A record** for the env's hostname (e.g. `dev.juggerhub.com`) → that IP.
cert-manager's HTTP-01 challenge self-heals once the record resolves — no re-apply.

Full validation steps: [`../specs/015-hosting/quickstart.md`](../specs/015-hosting/quickstart.md).

### Email deliverability — SPF / DKIM / DMARC (operator, manual)

Transactional mail is sent through **Resend** as `JuggerHub <hello@juggerhub.com>`
(`email_from_address`). Terraform does **not** manage the sending domain's DNS — only the
app's A record — so the records that make mail authenticate live at the registrar and must be
added by hand. **This is the single biggest cause of mail landing in spam.** If it is skipped,
every message fails SPF/DKIM/DMARC and Gmail/Yahoo/Outlook route it to junk regardless of what
the application does.

One-time setup:

1. In the Resend dashboard, add and verify the domain `juggerhub.com`. Resend shows the exact
   records to publish — typically an **SPF** `TXT` (`v=spf1 include:…resend… ~all`), a **DKIM**
   `TXT` on a Resend-provided selector (e.g. `resend._domainkey`), and often a return-path
   `CNAME`. Publish each at the registrar verbatim.
2. Publish a **DMARC** policy `TXT` at `_dmarc.juggerhub.com`, starting soft, e.g.
   `v=DMARC1; p=none; rua=mailto:hello@juggerhub.com`. Tighten to `p=quarantine` / `p=reject`
   once the aggregate reports show only legitimate mail passing.
3. Confirm the domain reads **Verified** in Resend (all rows green).

Verify from any machine (not from inside the app):

```bash
dig +short TXT juggerhub.com                    # SPF present, one v=spf1 record
dig +short TXT _dmarc.juggerhub.com             # DMARC present
dig +short TXT resend._domainkey.juggerhub.com  # DKIM key present (selector per Resend)
```

The application-side deliverability hardening (a `text/plain` alternative, a `List-Unsubscribe`
header, and an optional `Reply-To`) is already in the senders — see
[`../backend/Services/Email/EmailDelivery.cs`](../backend/Services/Email/EmailDelivery.cs) — but
it cannot compensate for a domain that fails authentication.

### Chat message encryption key (feature 047 / GH #223)

Chat message bodies are stored encrypted. The key is the GitHub Environment secret
**`CHAT_ENCRYPTION_KEYS`**, one value per environment, in the form
`version:base64key` — several entries separated by `;`, and **the first entry is the write
key**. Each key is exactly 32 bytes, base64-encoded:

```powershell
$b = [byte[]]::new(32)
[System.Security.Cryptography.RandomNumberGenerator]::Fill($b)
"1:" + [Convert]::ToBase64String($b)
```

The backend **refuses to start** without a usable key. There is no setting that turns encryption
off — that is deliberate, and a missing key is meant to stop the rollout rather than quietly
store message text in the clear.

> ⚠ **Restoring a database is not enough to restore chat.**
> The key is deliberately *not* in the database — that is the entire point, and it is what makes a
> stolen or mishandled database copy unreadable. A restore performed without the key that each row
> names produces a working platform in which **every stored message is permanently unreadable**.
> Treat `CHAT_ENCRYPTION_KEYS` as a required companion artefact of any backup, kept somewhere the
> database is not. Losing both loses all chat history, with no recovery path.

**Rotating** means prepending a new key and keeping the old one for as long as rows still name it:
`CHAT_ENCRYPTION_KEYS = "2:<new>;1:<old>"`. New messages take version 2; old ones keep reading.
Removing `1:` later makes every row still stamped with it unreadable — those messages then render
as *"This message can't be displayed."* rather than breaking their conversations.

### Postgres TLS certificates (feature 047)

The backend connects with `SSL Mode=VerifyFull`, so Postgres presents a certificate and the backend
verifies it. Both come from **cert-manager**, which the platform module already installs: a
self-signed `Issuer` bootstraps an internal CA (`postgres-ca`), which signs the server certificate
(`postgres-tls`). Nothing to configure and no secret to supply — but note two things:

- The certificate's SAN list **must contain the bare name `postgres`**, because the connection
  string says `Host=postgres` and `VerifyFull` matches the host as written, not the name it
  resolves to.
- `Trust Server Certificate` must never appear in the connection string. It would silently disable
  the verification this exists for.

Locally the equivalent is `./scripts/dev-postgres-certs.ps1`, which writes a CA and server
certificate into the gitignored `certs/local/`. It is idempotent and normally runs itself — the
Claude Code `SessionStart` hook generates the material when it is missing, and the `WorktreeCreate`
hook copies it into a new worktree alongside `.env`. Run the script by hand only on a plain
checkout that has neither.

#### Certificate lifetimes and how to rotate — read before shortening anything

Both certificates carry **explicit long durations**: the CA 10 years (renewed a year out), the
server certificate 5 years (renewed 30 days out, matching what the local script issues).

That is a deliberate deferral, not a default anyone forgot to change. cert-manager renews on its
own, but **PostgreSQL does not re-read `ssl_cert_file` by itself** — it is a `SIGHUP`-context
setting and nothing here sends a reload. With cert-manager's usual 90-day cadence, Postgres would
keep serving the *old* certificate after a renewal, silently self-heal if the pod happened to
restart inside the 30-day overlap, and otherwise start refusing connections about three months
after an apply with nothing in the diff to explain it. Tracked as **GH #239**; until that lands,
shortening these durations re-arms that failure.

**To rotate deliberately** (a suspected key compromise, or the five years running out):

```powershell
# 1. Delete the Secret; cert-manager re-issues from the Certificate resource within seconds.
kubectl -n juggerhub delete secret postgres-tls

# 2. Postgres will NOT pick it up on its own. Reload it — this re-reads the certificate
#    without dropping the database (a restart also works and is more disruptive).
kubectl -n juggerhub exec statefulset/postgres -- psql -U <user> -d <db> -c "SELECT pg_reload_conf();"

# 3. Verify what the SERVER now presents, not what the Secret contains.
kubectl -n juggerhub exec deployment/backend -- \
  openssl s_client -starttls postgres -connect postgres:5432 -showcerts </dev/null
```

Rotating the **CA** additionally requires deleting `postgres-ca`, letting the server certificate
re-issue beneath it, and restarting the backend so it picks up the new `ca.crt` — do that one in a
maintenance window, since it briefly invalidates the trust chain in both directions.

### Two-phase apply caveat (first run only)

The `kubernetes` and `helm` providers are configured from the AKS cluster's outputs,
and the Let's Encrypt `ClusterIssuer`s depend on cert-manager's CRDs — as do the Postgres TLS
`Issuer`/`Certificate` objects added by feature 047, which `kubernetes_manifest` resolves at
**plan** time. On a **brand-new** cluster you may need to create the cluster + platform first,
then the app:

```powershell
terraform apply -target=module.aks -target=module.platform -var-file=envs/dev.tfvars
terraform apply -var-file=envs/dev.tfvars -var="image_tag=<sha>"
```

Subsequent applies need no targeting.

---

## Workspaces & state isolation

- `terraform workspace select dev|prod|staging` scopes every plan/apply to that env.
- State lives in one container; each workspace is an isolated blob
  `env:/<env>/juggerhub.tfstate`.
- The `default` workspace is **guarded** — applying on it fails by design (FR-014).
- The state RG/storage is managed by the bootstrap script and is **never** touched by
  `terraform apply` (FR-013).

---

## Why a service principal?

GitHub Actions can't use your personal `az login`. It authenticates as a dedicated
**Entra ID app registration** via **federated OIDC** — GitHub mints a short-lived token
that Azure trusts, so there is **no stored client secret**. Federated credentials are
scoped per GitHub Environment (`…:environment:Dev`, `…:environment:Prod`) so Dev CI
cannot assume Prod access. Roles: Contributor (build infra) + User Access Administrator
(Terraform assigns roles, e.g. AKS→static-IP RG) + Storage Blob Data Contributor on the
state account (keyless backend). This matches the constitution: secrets in GitHub
Environments, no Azure Key Vault.

---

## Out of scope (own future features)

- Database **backup / restore / disaster recovery**.
- Delegating DNS to Azure DNS, **wildcard** certs, DNS-01 challenges.
