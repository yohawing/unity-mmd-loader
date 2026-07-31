function Read-NUnitTestRunSummary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [string] $Context = "NUnit test results"
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Context XML file was not created: $Path"
    }

    try {
        $xmlText = Get-Content -LiteralPath $Path -Raw
        if ([string]::IsNullOrWhiteSpace($xmlText)) {
            throw "the XML file is empty"
        }
        [xml] $resultsXml = $xmlText
    }
    catch {
        throw "$Context XML is malformed: $Path ($($_.Exception.Message))"
    }

    $testRun = $resultsXml.SelectSingleNode("/test-run")
    if ($null -eq $testRun) {
        throw "$Context XML has no <test-run> root: $Path"
    }

    $counts = @{}
    foreach ($attributeName in @("passed", "failed", "skipped", "total")) {
        $attribute = $testRun.Attributes[$attributeName]
        $value = 0
        if ($null -eq $attribute -or
            [string]::IsNullOrWhiteSpace([string] $attribute.Value) -or
            -not [int]::TryParse([string] $attribute.Value, [ref] $value) -or
            $value -lt 0) {
            throw "$Context XML has an invalid <$attributeName> count: $Path"
        }
        $counts[$attributeName] = $value
    }

    $resultAttribute = $testRun.Attributes["result"]
    $runResult = if ($null -eq $resultAttribute) { "" } else { [string] $resultAttribute.Value }
    if ([string]::IsNullOrWhiteSpace($runResult)) {
        throw "$Context XML has no test-run result: $Path"
    }

    [pscustomobject]@{
        Path = $Path
        Result = $runResult
        Passed = $counts["passed"]
        Failed = $counts["failed"]
        Skipped = $counts["skipped"]
        Total = $counts["total"]
        HasFailedResult = $runResult -eq "Failed" -or $runResult -eq "Failed(Child)"
    }
}
