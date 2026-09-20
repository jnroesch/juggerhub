<#
.SYNOPSIS
    Generates a VAPID key pair for Web Push (feature 055).

.DESCRIPTION
    Web Push identifies the application server with a P-256 key pair (VAPID, RFC 8292). The public
    half is handed to the browser when it subscribes and is baked into the subscription; the private
    half signs every delivery.

    The push library ships no generator and the encoding is specific, which is why this script
    exists rather than a comment telling the next person to find a tool:

      PublicKey  = base64url( 0x04 || X || Y )   the uncompressed P-256 point, 65 bytes
      PrivateKey = base64url( D )                the private scalar, 32 bytes

    Base64url here is standard base64 with '+' -> '-', '/' -> '_' and the '=' padding removed.

    EACH ENVIRONMENT GETS ITS OWN PAIR. A subscription is bound to the public key it was created
    with, so a key from one environment can never deliver to a subscription from another. Run this
    once for local development, once for Dev and once for Prod.

    The private key is a secret. Local: paste into .env, which is gitignored. Dev/Prod: store as the
    WEBPUSH_PRIVATE_KEY GitHub Environment secret. It must never be committed.

.EXAMPLE
    pwsh ./scripts/New-VapidKeyPair.ps1 -Subject "mailto:hello@juggerhub.com"
#>
[CmdletBinding()]
param(
    # VAPID 'sub' claim: how a push service reaches us about our traffic. Must be mailto: or https:.
    [string]$Subject = "mailto:hello@juggerhub.com"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not ($Subject.StartsWith("mailto:", "OrdinalIgnoreCase") -or $Subject.StartsWith("https://", "OrdinalIgnoreCase"))) {
    throw "Subject must start with 'mailto:' or 'https://' — push services reject anything else. Got: $Subject"
}

function ConvertTo-Base64Url {
    param([byte[]]$Bytes)
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$ecdsa = [System.Security.Cryptography.ECDsa]::Create(
    [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
try {
    $p = $ecdsa.ExportParameters($true)

    # The uncompressed point form the Web Push spec expects: a 0x04 marker then X then Y.
    $publicBytes = [byte[]]::new(1 + $p.Q.X.Length + $p.Q.Y.Length)
    $publicBytes[0] = 0x04
    [Array]::Copy($p.Q.X, 0, $publicBytes, 1, $p.Q.X.Length)
    [Array]::Copy($p.Q.Y, 0, $publicBytes, 1 + $p.Q.X.Length, $p.Q.Y.Length)

    $publicKey = ConvertTo-Base64Url -Bytes $publicBytes
    $privateKey = ConvertTo-Base64Url -Bytes $p.D
}
finally {
    $ecdsa.Dispose()
}

Write-Host ""
Write-Host "VAPID key pair generated. Paste these into .env for local development:" -ForegroundColor Green
Write-Host ""
Write-Host "WEBPUSH_SUBJECT=$Subject"
Write-Host "WEBPUSH_PUBLIC_KEY=$publicKey"
Write-Host "WEBPUSH_PRIVATE_KEY=$privateKey"
Write-Host ""
Write-Host "For Dev and Prod: Subject and PublicKey go in the Terraform ConfigMap," -ForegroundColor Yellow
Write-Host "PrivateKey goes in the WEBPUSH_PRIVATE_KEY GitHub Environment secret." -ForegroundColor Yellow
Write-Host "Never commit the private key." -ForegroundColor Yellow
Write-Host ""
