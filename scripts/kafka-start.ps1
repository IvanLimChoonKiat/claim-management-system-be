<#
.SYNOPSIS
  Starts a single-node Kafka broker (KRaft mode) for local development, using a native Kafka
  installation rather than Docker (no Docker on this dev machine - see docs/ARCHITECTURE.md and
  README.md "Running Kafka"). Blocks in the foreground; run it in its own terminal.

.PARAMETER KafkaHome
  Path to the Kafka distribution. Defaults to C:\kafka.

.PARAMETER JavaHome
  JDK to launch Kafka with. Kafka 4.x requires Java 17+; this machine's default `java` on PATH is
  1.8, so a specific JDK must be pointed at explicitly. Defaults to the Temurin 21 install found
  during setup.
#>
param(
    [string]$KafkaHome = "C:\kafka",
    [string]$JavaHome = "C:\Program Files\Eclipse Adoptium\jdk-21"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $repoRoot "data\kafka-logs"
$generatedConfigDir = Join-Path $repoRoot "data\kafka-config"
$templatePath = Join-Path $repoRoot "config\kafka\server.properties"
$resolvedConfigPath = Join-Path $generatedConfigDir "server.properties"
$binDir = Join-Path $KafkaHome "bin\windows"

if (-not (Test-Path $JavaHome)) {
    throw "JDK not found at '$JavaHome'. Kafka 4.x requires Java 17+ (this machine's default 'java' is 1.8). Pass -JavaHome to point at a suitable JDK."
}
if (-not (Test-Path $binDir)) {
    throw "Kafka distribution not found at '$KafkaHome'. Pass -KafkaHome, or install Kafka, or use docker-compose.yml instead (untested in this environment - see README.md)."
}

$env:JAVA_HOME = $JavaHome
Write-Host "Using JAVA_HOME=$JavaHome"

New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
New-Item -ItemType Directory -Force -Path $generatedConfigDir | Out-Null

# Java properties files are happiest with forward slashes even on Windows.
$logDirsForwardSlash = $dataDir.Replace('\', '/')
(Get-Content $templatePath) -replace '\{\{LOG_DIRS\}\}', $logDirsForwardSlash | Set-Content $resolvedConfigPath
Write-Host "Wrote resolved config to $resolvedConfigPath (log.dirs=$logDirsForwardSlash)"

$metaPropertiesPath = Join-Path $dataDir "meta.properties"
if (-not (Test-Path $metaPropertiesPath)) {
    Write-Host "No existing storage found - formatting (first run)..."
    $clusterId = & "$binDir\kafka-storage.bat" random-uuid
    $clusterId = ($clusterId | Select-Object -Last 1).Trim()
    Write-Host "Generated cluster id: $clusterId"

    & "$binDir\kafka-storage.bat" format --standalone --ignore-formatted --cluster-id $clusterId --config $resolvedConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "kafka-storage format failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Host "Existing storage found at $dataDir - skipping format."
}

Write-Host "Starting Kafka broker on localhost:9092 (Ctrl+C to stop)..."
& "$binDir\kafka-server-start.bat" $resolvedConfigPath
