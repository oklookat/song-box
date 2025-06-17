################
# Secrets file path
$secretFile = "$PSScriptRoot\secrets.json"

# List of secret variables with prompts and types
$secretVars = @(
    @{ Name = "sshIp"; Prompt = "Enter SSH server IP"; Type = "string" },
    @{ Name = "sshUser"; Prompt = "Enter SSH user"; Type = "string" },
    @{ Name = "sshHomePath"; Prompt = "Enter home path (e.g. /home/USERNAME/song-box)"; Type = "string" },
    @{ Name = "sshPublicDir"; Prompt = "Enter public dir (e.g. /usr/share/nginx/song-box)"; Type = "string" },
    @{ Name = "sshPort"; Prompt = "Enter SSH port"; Type = "int" },
    @{ Name = "zipUrl"; Prompt = "Enter public .zip URL (e.g. /usr/share/nginx/song-box/song-box.zip)"; Type = "string" }
)

function SecureStringToPlainText($secureStr) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureStr)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Read-And-EncryptSecrets {
    param($vars)
    $result = @{}
    foreach ($var in $vars) {
        $someInput = Read-Host $var.Prompt -AsSecureString
        $result[$var.Name] = $someInput | ConvertFrom-SecureString
    }
    return $result
}

function Invoke-Load-And-DecryptSecrets {
    param($vars, $json)
    $result = @{}
    foreach ($var in $vars) {
        $secure = $json.$($var.Name) | ConvertTo-SecureString
        $plain = SecureStringToPlainText $secure
        if ($var.Type -eq "int") {
            $plain = [int]$plain
        }
        $result[$var.Name] = $plain
    }
    return $result
}

if (-Not (Test-Path $secretFile)) {
    $encryptedSecrets = Read-And-EncryptSecrets -vars $secretVars
    $encryptedSecrets | ConvertTo-Json | Out-File $secretFile -Encoding UTF8
    Write-Host "Secrets saved."
}

$secretsJson = Get-Content $secretFile | ConvertFrom-Json
$secrets = Invoke-Load-And-DecryptSecrets -vars $secretVars -json $secretsJson

# Assign each secret to its own variable
foreach ($var in $secretVars) {
    Set-Variable -Name $var.Name -Value $secrets[$var.Name]
}
################

# Include this files from script dir.
$includeFiles = @("song-box.json")
$zipName = "song-box.zip"
$updaterExe = "song-box-updater.exe"
$singBoxConfig = "sing-box.json"
$releaseDir = "..\bin\x64\Release"
$scriptDir = $PSScriptRoot

Write-Host "Script directory: $scriptDir"

if (-not (Test-Path $singBoxConfig)) {
    Write-Error "Required file '$file' not found in script directory."
    Invoke-Pause-Exit
    exit 1
}

# Function to pause with custom message
function Invoke-Pause-Exit($message = "Press Ctrl+C to exit...") {
    Write-Host $message -ForegroundColor Yellow
    while ($true) {
        Start-Sleep -Seconds 1
    }
}

function Invoke-SecureCopy {
    param(
        [string]$localFilePath,
        [string]$remotePath
    )

    # Формируем удалённый адрес с правильной подстановкой
    $remoteTarget = "${sshUser}@${sshIp}:$remotePath"

    # Выполняем scp с указанием порта
    scp -P $sshPort $localFilePath $remoteTarget
}

function Invoke-SudoRemoteCommand {
    param(
        [string]$remoteCommand
    )

    $remoteTarget = "${sshUser}@${sshIp}"

    ssh -t -p $sshPort $remoteTarget "sudo $remoteCommand"
}

function Invoke-RemoteCommand {
    param(
        [string]$remoteCommand
    )

    $remoteTarget = "${sshUser}@${sshIp}"

    ssh -p $sshPort $remoteTarget "$remoteCommand"
}


# Build the updater
Write-Host "Building updater..."
Set-Location ..\song-box-updater
go build

# Move the built updater executable to Release folder
Move-Item -Path $updaterExe -Destination $releaseDir -Force
Write-Host "Updater moved to Release folder."

# Check for required files in script directory
Set-Location $scriptDir
foreach ($file in $includeFiles) {
    if (-not (Test-Path $file)) {
        Write-Error "Required file '$file' not found in script directory."
        Invoke-Pause-Exit
        exit 1
    }
}
# Copy files to Release directory
foreach ($file in $jsonFiles) {
    Copy-Item -Path $file -Destination $releaseDir -Force
}
Write-Host "Include files copied to Release folder."

# Create ZIP archive of all files in Release folder
Set-Location $releaseDir
# Get version of binary.
$fileVersionInfo = (Get-Item "song-box.exe").VersionInfo.FileVersion
Write-Host "File version string: $fileVersionInfo"
Write-Host "Creating ZIP archive '$zipName' from Release folder contents..."
Compress-Archive -Path * -DestinationPath $zipName -Force

# Move ZIP back to script directory
Move-Item -Path $zipName -Destination $scriptDir -Force
Write-Host "ZIP archive moved back to script directory."

# Get ZIP hash.
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$fileStream = [System.IO.File]::OpenRead($zipName)
$hashBytes = $sha256.ComputeHash($fileStream)
$fileStream.Close()
$hashString = ($hashBytes | ForEach-Object { $_.ToString("x2") }) -join ""
Write-Host "SHA256 hash:" $hashString

# Set update json.
$versionArray = $fileVersionInfo -split '\.' | ForEach-Object { [int]$_ }
$updateJsonPath = "$scriptDir\update.json"
$updateJsonObject = [PSCustomObject]@{
    version   = $versionArray
    zipSha256 = $hashString
    zipUrl    = $zipUrl
}
$newUpdateJson = $updateJsonObject | ConvertTo-Json -Depth 10
Set-Content -Path $updateJsonPath -Value $newUpdateJson
Write-Host "JSON file updated with version from exe."

# Copy to server (home dir)
Invoke-RemoteCommand -remoteCommand "mkdir -p $sshHomePath"
Invoke-SecureCopy -localFilePath "$scriptDir\$zipName" -remotePath $sshHomePath
Invoke-SecureCopy -localFilePath "$updateJsonPath" -remotePath $sshHomePath
Invoke-SecureCopy -localFilePath "$scriptDir\$singBoxConfig" -remotePath $sshHomePath

# Copy from home to nginx static
Invoke-SudoRemoteCommand -remoteCommand "rm -rf $sshPublicDir"
Invoke-SudoRemoteCommand -remoteCommand "mv $sshHomePath $sshPublicDir"

# Pause to allow user to see output
Write-Host "Press any key to exit..."
[void][System.Console]::ReadKey($true)