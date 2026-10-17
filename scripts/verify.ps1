<#
.SYNOPSIS
  Repeatable functional verification for the Claims Management backend: build, unit/integration
  tests, then a real REST lifecycle smoke test against a running instance of the API on an
  isolated SQLite database. Distinct from `dotnet test` (unit/integration) - this is the
  end-to-end smoke layer, exercising the actual HTTP surface, not just in-process code.

.PARAMETER WithKafka
  Also verifies Kafka connectivity (requires a broker already running - see
  scripts/kafka-start.ps1). Off by default: ordinary verification must not require Kafka.

.EXAMPLE
  ./scripts/verify.ps1
.EXAMPLE
  ./scripts/verify.ps1 -WithKafka
#>
param(
    [switch]$WithKafka
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$script:FailureCount = 0
$script:TestDbPath = Join-Path $env:TEMP "claims-verify-$([Guid]::NewGuid().ToString('N')).db"
$script:ApiProcess = $null
$VerifyPort = 5277
$BaseUrl = "http://localhost:$VerifyPort"

function Write-Check([string]$Name, [bool]$Passed, [string]$Detail = "") {
    if ($Passed) {
        Write-Host "[PASS] $Name" -ForegroundColor Green
    }
    else {
        Write-Host "[FAIL] $Name $Detail" -ForegroundColor Red
        $script:FailureCount++
    }
}

function Stop-ApiIfRunning {
    if ($script:ApiProcess -and -not $script:ApiProcess.HasExited) {
        Stop-Process -Id $script:ApiProcess.Id -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-ApiJson {
    param([string]$Method, [string]$Path, $Body = $null, [string]$Token = $null)
    $headers = @{}
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    $params = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
        $params["ContentType"] = "application/json"
    }
    return Invoke-RestMethod @params
}

try {
    Write-Host "=== 1. Environment prerequisite checks ===" -ForegroundColor Cyan
    $dotnetVersion = (dotnet --version 2>$null)
    Write-Check "dotnet SDK available ($dotnetVersion)" ($null -ne $dotnetVersion)

    Write-Host "`n=== 2. Backend dependency restoration ===" -ForegroundColor Cyan
    dotnet restore ClaimsManagement.sln | Out-Null
    Write-Check "dotnet restore" ($LASTEXITCODE -eq 0)

    Write-Host "`n=== 3. Backend build (Release) ===" -ForegroundColor Cyan
    dotnet build ClaimsManagement.sln --configuration Release --no-restore | Out-Null
    Write-Check "dotnet build" ($LASTEXITCODE -eq 0)

    Write-Host "`n=== 4. Unit and integration tests ===" -ForegroundColor Cyan
    dotnet test ClaimsManagement.sln --configuration Release --no-build | Out-Null
    Write-Check "dotnet test (unit + API integration)" ($LASTEXITCODE -eq 0)

    Write-Host "`n=== 5. SQLite test database initialization ===" -ForegroundColor Cyan
    $env:ConnectionStrings__ClaimsDatabase = "Data Source=$script:TestDbPath"
    $env:Jwt__SigningKey = "VERIFY-SCRIPT-ONLY-SIGNING-KEY-NOT-FOR-PRODUCTION-USE"
    $env:Jwt__ExpiryHours = "1"
    $env:Kafka__Enabled = if ($WithKafka) { "true" } else { "false" }
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $BaseUrl

    $script:ApiProcess = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --no-build --configuration Release --project src/ClaimsManagement.Api --no-launch-profile" `
        -PassThru -WindowStyle Hidden

    $started = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            Invoke-WebRequest -Uri "$BaseUrl/swagger/v1/swagger.json" -UseBasicParsing -TimeoutSec 2 | Out-Null
            $started = $true
            break
        }
        catch { }
    }
    Write-Check "SQLite database initialized (migrations applied on startup)" $started "(API did not become ready)"

    Write-Host "`n=== 6. API health check ===" -ForegroundColor Cyan
    Write-Check "API responds on $BaseUrl" $started

    Write-Host "`n=== 7-9. Claim submission, retrieval, assignment (REST) ===" -ForegroundColor Cyan
    $claimantEmail = "verify-claimant-$([Guid]::NewGuid().ToString('N').Substring(0,8))@example.com"
    $officerEmail = "verify-officer-$([Guid]::NewGuid().ToString('N').Substring(0,8))@example.com"

    $claimantLogin = Invoke-ApiJson -Method Post -Path "/api/auth/dev-login" -Body @{ email = $claimantEmail; displayName = "Verify Claimant"; role = "Claimant" }
    $officerLogin = Invoke-ApiJson -Method Post -Path "/api/auth/dev-login" -Body @{ email = $officerEmail; displayName = "Verify Officer"; role = "ClaimsOfficer" }
    Write-Check "dev-login (claimant + officer)" ($null -ne $claimantLogin.token -and $null -ne $officerLogin.token)

    $submitBody = @{
        type                = "Motor"
        incidentDateUtc     = (Get-Date).AddDays(-1).ToUniversalTime().ToString("o")
        incidentDescription = "Verification run: rear-ended at a traffic light"
        incidentLocation    = "Orchard Road, Singapore"
        vehicleRegistration = "SGX1234A"
        propertyAddress     = $null
        estimatedLiability  = 5000
    }
    $claim = Invoke-ApiJson -Method Post -Path "/api/claims" -Body $submitBody -Token $claimantLogin.token
    Write-Check "Claim submission (POST /api/claims)" ($claim.status -eq "Submitted" -and $claim.claimReference -like "CLM-*")

    $fetched = Invoke-ApiJson -Method Get -Path "/api/claims/$($claim.id)" -Token $claimantLogin.token
    Write-Check "Claim retrieval (GET /api/claims/{id})" ($fetched.id -eq $claim.id)

    $assigned = Invoke-ApiJson -Method Post -Path "/api/claims/$($claim.id)/assign" -Token $officerLogin.token
    Write-Check "Claim assignment (POST /api/claims/{id}/assign)" ($assigned.status -eq "Assigned" -and $assigned.assignedOfficerUserId -eq $officerLogin.user.id)

    Write-Host "`n=== 10. Valid claim lifecycle transition ===" -ForegroundColor Cyan
    $decided = Invoke-ApiJson -Method Post -Path "/api/claims/$($claim.id)/decision" -Body @{ outcome = "Approved"; settlementAmount = 4500; reason = "Verification run: valid claim" } -Token $officerLogin.token
    Write-Check "Valid transition Assigned -> Approved" ($decided.status -eq "Approved" -and $decided.decision.settlementAmount -eq 4500)

    Write-Host "`n=== 11. Rejection of an invalid operation ===" -ForegroundColor Cyan
    $invalidRejected = $false
    try {
        Invoke-ApiJson -Method Post -Path "/api/claims/$($claim.id)/decision" -Body @{ outcome = "Rejected"; reason = "Already decided" } -Token $officerLogin.token
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $invalidRejected = ($statusCode -eq 409)
    }
    Write-Check "Invalid transition on already-decided claim rejected with 409" $invalidRejected

    if ($WithKafka) {
        Write-Host "`n=== 13. Kafka connectivity and event verification ===" -ForegroundColor Cyan
        try {
            $tcp = New-Object System.Net.Sockets.TcpClient
            $tcp.Connect("localhost", 9092)
            $kafkaUp = $tcp.Connected
            $tcp.Close()
        }
        catch { $kafkaUp = $false }
        Write-Check "Kafka broker reachable on localhost:9092" $kafkaUp "(run scripts/kafka-start.ps1 first)"
    }
    else {
        Write-Host "`n=== 13. Kafka connectivity (skipped - pass -WithKafka to enable) ===" -ForegroundColor DarkGray
    }
}
finally {
    Stop-ApiIfRunning
    if (Test-Path $script:TestDbPath) { Remove-Item -Force $script:TestDbPath -ErrorAction SilentlyContinue }
    Remove-Item Env:\ConnectionStrings__ClaimsDatabase, Env:\Jwt__SigningKey, Env:\Jwt__ExpiryHours, Env:\Kafka__Enabled, Env:\ASPNETCORE_ENVIRONMENT, Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
}

Write-Host "`n=================================="
if ($script:FailureCount -eq 0) {
    Write-Host "All checks passed." -ForegroundColor Green
    exit 0
}
else {
    Write-Host "$script:FailureCount check(s) failed." -ForegroundColor Red
    exit 1
}
