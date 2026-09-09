#Requires -Version 7.0
<#
.SYNOPSIS
    Generates the local CA and PostgreSQL server certificate used by docker-compose (feature 047).

.DESCRIPTION
    Feature 047 requires the backend to connect to PostgreSQL with `SSL Mode=VerifyFull`, which
    means two things must exist locally: a certificate the database presents, and a CA the backend
    trusts. Deployed environments get both from cert-manager; locally this script is the equivalent.

    Nothing it writes is ever committed — `certs/` is in .gitignore. A private key in public git
    history cannot be revoked, and a committed development CA would be a certificate authority
    every clone of the repository trusts.

    Written against .NET's CertificateRequest API rather than shelling out to `openssl`, which is
    not present on a stock Windows machine. Constitution Principle VI: .ps1 only.

    The SAN list matters more than it looks. `VerifyFull` checks the host name exactly as it appears
    in the connection string, so all three spellings the repository actually uses must be present:
      database   the docker-compose service name, used by the backend container
      localhost  used by `dotnet run` and `dotnet ef` from the host
      127.0.0.1  the same, when something resolves it numerically

.PARAMETER Force
    Regenerate even when a valid, unexpired certificate is already present.

.EXAMPLE
    ./scripts/dev-postgres-certs.ps1
#>
[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'certs/local'

$caCrtPath = Join-Path $outDir 'ca.crt'
$serverCrtPath = Join-Path $outDir 'server.crt'
$serverKeyPath = Join-Path $outDir 'server.key'

# --- Idempotence ------------------------------------------------------------
# Re-running must be free. Regenerating on every call would rotate the CA underneath a running
# stack and break the backend's trust store for no reason.
if (-not $Force -and (Test-Path $caCrtPath) -and (Test-Path $serverCrtPath) -and (Test-Path $serverKeyPath)) {
    # CreateFromPem, not CreateFromPemFile: the latter wants the private key alongside the
    # certificate and throws on a certificate-only PEM.
    $existing = [System.Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem(
        (Get-Content -Raw -Path $serverCrtPath))
    try {
        if ($existing.NotAfter -gt (Get-Date).AddDays(30)) {
            Write-Host "Certificates already present and valid until $($existing.NotAfter.ToString('yyyy-MM-dd')) — nothing to do."
            Write-Host "  $outDir"
            Write-Host 'Re-run with -Force to regenerate.'
            return
        }
        Write-Host "Existing certificate expires $($existing.NotAfter.ToString('yyyy-MM-dd')) — regenerating."
    }
    finally {
        $existing.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$notBefore = [DateTimeOffset]::UtcNow.AddDays(-1)
$notAfter = [DateTimeOffset]::UtcNow.AddYears(5)

# --- The CA -----------------------------------------------------------------
$caKey = [System.Security.Cryptography.RSA]::Create(4096)
try {
    $caRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=JuggerHub Local Development CA,O=JuggerHub',
        $caKey,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    $caRequest.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true, $false, 0, $true))
    $caRequest.CertificateExtensions.Add(
        [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor
            [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign, $true))

    $ca = $caRequest.CreateSelfSigned($notBefore, $notAfter)
    try {
        # --- The server certificate ------------------------------------------
        $serverKey = [System.Security.Cryptography.RSA]::Create(2048)
        try {
            $serverRequest = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
                'CN=database,O=JuggerHub',
                $serverKey,
                [System.Security.Cryptography.HashAlgorithmName]::SHA256,
                [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

            $serverRequest.CertificateExtensions.Add(
                [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false, $false, 0, $true))
            $serverRequest.CertificateExtensions.Add(
                [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                    [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor
                    [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment, $true))
            # OidCollection has no collection initialiser PowerShell can coerce an array into —
            # it must be built by Add(), or the cast fails at runtime.
            $eku = [System.Security.Cryptography.OidCollection]::new()
            $eku.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1')) | Out-Null # serverAuth
            $serverRequest.CertificateExtensions.Add(
                [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($eku, $false))

            # See the SAN note in the header — VerifyFull matches the host string as written.
            $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
            $san.AddDnsName('database')
            $san.AddDnsName('localhost')
            $san.AddIpAddress([System.Net.IPAddress]::Parse('127.0.0.1'))
            $serverRequest.CertificateExtensions.Add($san.Build())

            $serial = [byte[]]::new(16)
            [System.Security.Cryptography.RandomNumberGenerator]::Fill($serial)

            $server = $serverRequest.Create($ca, $notBefore, $notAfter.AddDays(-1), $serial)
            try {
                # PEM throughout: PostgreSQL's ssl_cert_file/ssl_key_file and Npgsql's
                # `Root Certificate` all read PEM, and no PKCS#12 password has to be invented.
                Set-Content -Path $caCrtPath -Value ($ca.ExportCertificatePem()) -Encoding ascii
                Set-Content -Path $serverCrtPath -Value ($server.ExportCertificatePem()) -Encoding ascii
                Set-Content -Path $serverKeyPath -Value ($serverKey.ExportPkcs8PrivateKeyPem()) -Encoding ascii
            }
            finally {
                $server.Dispose()
            }
        }
        finally {
            $serverKey.Dispose()
        }
    }
    finally {
        $ca.Dispose()
    }
}
finally {
    $caKey.Dispose()
}

Write-Host 'Wrote:'
Write-Host "  $caCrtPath      (mounted into the backend; trusted via `Root Certificate`)"
Write-Host "  $serverCrtPath  (presented by Postgres)"
Write-Host "  $serverKeyPath  (copied to 0600 inside the container — see docker-compose.yml)"
Write-Host ''
Write-Host 'Valid until ' -NoNewline
Write-Host $notAfter.ToString('yyyy-MM-dd')
Write-Host 'These files are gitignored. Never commit them.'
