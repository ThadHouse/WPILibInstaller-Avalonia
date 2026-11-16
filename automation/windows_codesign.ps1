param(
    [Parameter(Mandatory = $true)]
    [string]$DirectoryToSign
)

Set-StrictMode -Version 'Latest'
$PSDefaultParameterValues['*:ErrorAction'] = 'Stop'
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$requiredVariables = @(
    'AZURE_SIGNING_ENDPOINT',
    'AZURE_SIGNING_ACCOUNT_NAME',
    'AZURE_SIGNING_PROFILE_NAME',
    'AZURE_CLIENT_ID',
    'AZURE_TENANT_ID',
    'AZURE_CLIENT_SECRET'
)
foreach ($variable in $requiredVariables)
{
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variable)))
    {
        throw "Windows signing requires the $variable environment variable."
    }
}

$files = @(Get-ChildItem -LiteralPath $DirectoryToSign -File -Recurse |
    Where-Object { $_.Extension -in '.exe', '.dll' })
if ($files.Count -eq 0)
{
    throw "No Windows executables or DLLs found in $DirectoryToSign."
}

Install-Module -Name TrustedSigning -Force -Scope CurrentUser -AllowClobber

foreach ($file in $files)
{
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid)
    {
        Write-Host "File $($file.FullName) is already signed and valid."
        continue
    }

    Write-Host "Signing file $($file.FullName)..."
    $parameters = @{
        Endpoint = $env:AZURE_SIGNING_ENDPOINT
        CodeSigningAccountName = $env:AZURE_SIGNING_ACCOUNT_NAME
        CertificateProfileName = $env:AZURE_SIGNING_PROFILE_NAME
        FileDigest = 'SHA256'
        Files = $file.FullName
        TimestampRfc3161 = 'http://timestamp.acs.microsoft.com'
        TimestampDigest = 'SHA256'
    }
    Invoke-TrustedSigning @parameters -Verbose

    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid)
    {
        throw "Signature verification failed for $($file.FullName): $($signature.StatusMessage)"
    }
}
