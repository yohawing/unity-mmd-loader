# Shared helpers for every script that launches Unity in batchmode.
#
# Unity.exe is a GUI-subsystem executable. The PowerShell call operator
# (`& $Unity ...`) returns before the spawned process exits, so $LASTEXITCODE is
# $null/stale and result files may not exist yet. Treating that $null as exit 0
# made the gates report "passed" while Unity was still running (or had failed).
# Invoke-UnityBatch blocks on the real Unity process handle (Start-Process -PassThru +
# WaitForExit) and
# returns Unity's actual exit code; callers must treat any non-zero code as failure.

. (Join-Path $PSScriptRoot "unity-process-environment.ps1")
Initialize-UnityProcessEnvironment
. (Join-Path $PSScriptRoot "unity-project-guard.ps1")

# Single source of truth for the Unity editor used by the CLI gates (AGENTS.md Local Facts).
$script:DefaultUnityExecutable = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe"

function Resolve-UnityExecutable {
    param([string] $Unity = "")

    $candidate = if ([string]::IsNullOrWhiteSpace($Unity)) { $script:DefaultUnityExecutable } else { $Unity }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Unity executable not found: $candidate"
    }

    # Resolve against the PowerShell location (not the process working directory) so a
    # relative -Unity path that Test-Path accepted is launched from the same place.
    return (Resolve-Path -LiteralPath $candidate).ProviderPath
}

function Resolve-OutputPath {
    param([Parameter(Mandatory = $true)][string] $Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

# Quotes one argument using the Windows CommandLineToArgvW rules so paths with
# spaces (or embedded quotes) reach Unity as a single argv entry.
function ConvertTo-UnityCommandLineArgument {
    param([AllowEmptyString()][string] $Argument)

    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') {
        return $Argument
    }

    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') {
            $backslashes++
            continue
        }

        if ($character -eq '"') {
            [void] $builder.Append([char]'\', ($backslashes * 2) + 1)
        }
        elseif ($backslashes -gt 0) {
            [void] $builder.Append([char]'\', $backslashes)
        }
        $backslashes = 0
        [void] $builder.Append($character)
    }
    if ($backslashes -gt 0) {
        [void] $builder.Append([char]'\', $backslashes * 2)
    }
    [void] $builder.Append('"')
    return $builder.ToString()
}

# Launches Unity, waits for the process to exit, and returns its exit code as [int].
# Throws when the process cannot be started or its exit code is unavailable; never
# substitutes 0 for a missing exit code.
function Invoke-UnityBatch {
    param(
        [Parameter(Mandatory = $true)][string] $Unity,
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $quotedArguments = @($Arguments | ForEach-Object { ConvertTo-UnityCommandLineArgument $_ })
    # Do not use Start-Process -Wait: it waits for the whole descendant tree, and after a
    # real C# compile Unity leaves the Roslyn compiler server (dotnet csc /shared) alive
    # for its ~10 minute idle timeout, which stalled a compile-error run by 600s.
    # Wait on the Unity process handle itself instead. Touching .Handle right after the
    # start keeps the handle open so ExitCode is still readable after the process exits.
    $process = Start-Process -FilePath $Unity -ArgumentList $quotedArguments -PassThru -NoNewWindow
    if ($null -eq $process) {
        throw "Unity process could not be started: $Unity"
    }
    $null = $process.Handle
    $process.WaitForExit()
    if ($null -eq $process.ExitCode) {
        throw "Unity process exit code is unavailable: $Unity $($quotedArguments -join ' ')"
    }

    return [int] $process.ExitCode
}

# Waits for Unity processes that have this project open to finish and removes stale
# asset database locks so the next batchmode run can open the project. Editors open on
# other projects are ignored: the conflict unit is the project, not the machine.
function Clear-StaleUnityProjectLocks {
    param([Parameter(Mandatory = $true)][string] $ProjectPath)

    for ($i = 0; $i -lt 240; $i++) {
        if (@(Get-UnityProcessesForProject -ProjectPath $ProjectPath).Count -eq 0) {
            break
        }
        Start-Sleep -Milliseconds 250
    }

    if (@(Get-UnityProcessesForProject -ProjectPath $ProjectPath).Count -gt 0) {
        return
    }

    Remove-Item -LiteralPath `
        (Join-Path $ProjectPath "Library\ArtifactDB-lock"), `
        (Join-Path $ProjectPath "Library\SourceAssetDB-lock") `
        -Force -ErrorAction SilentlyContinue
}

# Belt-and-suspenders only: Invoke-UnityBatch already waited for the process, so the
# exit code and result XML are the authoritative signals. These short waits tolerate
# filesystem flush latency and return immediately when the file is already stable.
function Wait-ForFile {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int] $TimeoutSeconds = 10
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return $true
        }
        Start-Sleep -Milliseconds 250
    }

    return (Test-Path -LiteralPath $Path)
}

function Wait-ForFileToSettle {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [int] $TimeoutSeconds = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastLength = -1
    $lastWriteTimeUtc = [datetime]::MinValue
    $stableCount = 0
    while ((Get-Date) -lt $deadline) {
        $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
        if ($null -eq $item) {
            Start-Sleep -Milliseconds 250
            continue
        }

        if ($item.Length -eq $lastLength -and $item.LastWriteTimeUtc -eq $lastWriteTimeUtc) {
            $stableCount++
            if ($stableCount -ge 4) {
                return $true
            }
        }
        else {
            $stableCount = 0
            $lastLength = $item.Length
            $lastWriteTimeUtc = $item.LastWriteTimeUtc
        }

        Start-Sleep -Milliseconds 250
    }

    return (Test-Path -LiteralPath $Path)
}
