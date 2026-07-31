function ConvertTo-UnityProjectGuardPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    return $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar).Replace('/', '\').ToLowerInvariant()
}

function Get-UnityProjectProcessKind {
    param([string] $CommandLine)

    if ($CommandLine -match '(?i)AssetImportWorker' -or $CommandLine -match '(?i)-name"?\s+"?AssetImport') {
        return "AssetImportWorker"
    }

    if ($CommandLine -match '(?i)-batchmode') {
        return "BatchMode"
    }

    return "Editor"
}

function Get-UnityProcessesForProject {
    param([Parameter(Mandatory = $true)][string] $ProjectPath)

    $canonicalProjectPath = ConvertTo-UnityProjectGuardPath -Path $ProjectPath
    $processes = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" -ErrorAction SilentlyContinue
    foreach ($process in $processes) {
        $commandLine = [string] $process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }

        $normalizedCommandLine = $commandLine.Replace('/', '\').ToLowerInvariant()
        if (-not $normalizedCommandLine.Contains($canonicalProjectPath)) {
            continue
        }

        [pscustomobject] @{
            ProcessId = $process.ProcessId
            Kind = Get-UnityProjectProcessKind -CommandLine $commandLine
            CreationDate = $process.CreationDate
            CommandLine = $commandLine
        }
    }
}

function Assert-NoRunningUnityProject {
    param(
        [Parameter(Mandatory = $true)][string] $ProjectPath,
        [Parameter(Mandatory = $true)][string] $OperationName,
        [int] $TransientWaitSeconds = 120
    )

    $matches = @(Get-UnityProcessesForProject -ProjectPath $ProjectPath)
    $editorMatches = @($matches | Where-Object { $_.Kind -eq "Editor" })
    if ($matches.Count -gt 0 -and $TransientWaitSeconds -gt 0 -and $editorMatches.Count -eq 0) {
        $deadline = (Get-Date).AddSeconds($TransientWaitSeconds)
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $matches = @(Get-UnityProcessesForProject -ProjectPath $ProjectPath)
            $editorMatches = @($matches | Where-Object { $_.Kind -eq "Editor" })
            if ($matches.Count -eq 0 -or $editorMatches.Count -gt 0) {
                break
            }
        }
    }

    if ($matches.Count -eq 0) {
        return
    }

    $projectFullPath = [System.IO.Path]::GetFullPath($ProjectPath)
    $processLines = $matches | ForEach-Object {
        "pid={0}; kind={1}; started={2}" -f $_.ProcessId, $_.Kind, $_.CreationDate
    }

    throw @"
$OperationName cannot run while Unity already has this project open.
project=$projectFullPath
processes:
$($processLines -join [Environment]::NewLine)

Close the Unity Editor for this project and retry the release gate.
"@
}
