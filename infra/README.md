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

### Two-phase apply caveat (first run only)

The `kubernetes` and `helm` providers are configured from the AKS cluster's outputs,
and the Let's Encrypt `ClusterIssuer`s depend on cert-manager's CRDs. On a **brand-new**
cluster you may need to create the cluster + platform first, then the app:

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
