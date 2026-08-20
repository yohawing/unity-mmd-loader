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
    foreach ($attributeName in @("inconclusive", "invalid")) {
        $attribute = $testRun.Attributes[$attributeName]
        $value = 0
        if ($null -ne $attribute -and (
            [string]::IsNullOrWhiteSpace([string] $attribute.Value) -or
            -not [int]::TryParse([string] $attribute.Value, [ref] $value) -or
            $value -lt 0)) {
            throw "$Context XML has an invalid <$attributeName> count: $Path"
        }
        $counts[$attributeName] = $value
    }

    $resultAttribute = $testRun.Attributes["result"]
    $runResult = if ($null -eq $resultAttribute) { "" } else { [string] $resultAttribute.Value }
    if ([string]::IsNullOrWhiteSpace($runResult)) {
        throw "$Context XML has no test-run result: $Path"
    }

    $skippedTestRecords = @(
        $testRun.SelectNodes(".//test-case[@result='Skipped']") | ForEach-Object {
            $nameAttribute = $_.Attributes["fullname"]
            if ($null -eq $nameAttribute) {
                $nameAttribute = $_.Attributes["name"]
            }
            if ($null -eq $nameAttribute) {
                $nameAttribute = $_.Attributes["id"]
            }
            if ($null -eq $nameAttribute -or [string]::IsNullOrWhiteSpace([string] $nameAttribute.Value)) {
                throw "$Context XML has a skipped test case without fullname, name, or id: $Path"
            }
            [pscustomobject] @{
                Name = [string] $nameAttribute.Value
                RunState = if ($null -eq $_.Attributes["runstate"]) { "" } else { [string] $_.Attributes["runstate"].Value }
            }
        }
    )
    $skippedTestNames = @($skippedTestRecords | ForEach-Object { $_.Name })
    $explicitSkippedTestNames = @(
        $skippedTestRecords |
            Where-Object { $_.RunState -eq "Explicit" } |
            ForEach-Object { $_.Name }
    )

    [pscustomobject]@{
        Path = $Path
        Result = $runResult
        Passed = $counts["passed"]
        Failed = $counts["failed"]
        Skipped = $counts["skipped"]
        Inconclusive = $counts["inconclusive"]
        Invalid = $counts["invalid"]
        Total = $counts["total"]
        SkippedTestNames = $skippedTestNames
        ExplicitSkippedTestNames = $explicitSkippedTestNames
        HasFailedResult = $runResult -eq "Failed" -or $runResult -eq "Failed(Child)"
    }
}

function Assert-NUnitTestRunEvidence {
    <#
    .SYNOPSIS
        Applies the strict evidence policy to a parsed NUnit test-run summary.

    .DESCRIPTION
        Parsing and policy are intentionally separate. Read-NUnitTestRunSummary
        remains suitable for report-only callers, while this assertion rejects
        incomplete or non-passing evidence. Skips are accepted only when every
        skipped test has a name present in AllowedSkippedTests, and a skipped
        run result is accepted only as Skipped or Skipped:Ignored. No wildcard
        or count-only skip policy is supported.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)][psobject] $Summary,
        [Parameter(Mandatory = $true)][ValidateRange(0, [int]::MaxValue)][int] $MinimumTotal,
        [Parameter(Mandatory = $true)][ValidateRange(0, [int]::MaxValue)][int] $MinimumPassed,
        [string[]] $AllowedSkippedTests = @(),
        [string] $Context = "NUnit test evidence"
    )

    if ($null -eq $Summary) {
        throw "$Context summary is missing"
    }

    foreach ($propertyName in @("Path", "Result", "Passed", "Failed", "Skipped", "Total")) {
        $property = $Summary.PSObject.Properties[$propertyName]
        if ($null -eq $property) {
            throw "$Context summary is missing the '$propertyName' property"
        }
    }
    if ([string]::IsNullOrWhiteSpace([string] $Summary.Path)) {
        throw "$Context summary has no source path"
    }

    $counts = @{}
    foreach ($propertyName in @("Passed", "Failed", "Skipped", "Total")) {
        $value = 0
        if ($null -eq $Summary.$propertyName -or
            -not [int]::TryParse([string] $Summary.$propertyName, [ref] $value) -or
            $value -lt 0) {
            throw "$Context summary has an invalid $propertyName count: $($Summary.Path)"
        }
        $counts[$propertyName] = $value
    }
    foreach ($propertyName in @("Inconclusive", "Invalid")) {
        $property = $Summary.PSObject.Properties[$propertyName]
        if ($null -eq $property) {
            $counts[$propertyName] = 0
            continue
        }

        $value = 0
        if ($null -eq $Summary.$propertyName -or
            -not [int]::TryParse([string] $Summary.$propertyName, [ref] $value) -or
            $value -lt 0) {
            throw "$Context summary has an invalid $propertyName count: $($Summary.Path)"
        }
        $counts[$propertyName] = $value
    }

    if ([string]::IsNullOrWhiteSpace([string] $Summary.Result)) {
        throw "$Context summary has no test-run result: $($Summary.Path)"
    }
    if ($counts["Inconclusive"] -gt 0) {
        throw "$Context contains inconclusive tests: inconclusive=$($counts["Inconclusive"]); path=$($Summary.Path)"
    }
    if ($counts["Invalid"] -gt 0) {
        throw "$Context contains invalid tests: invalid=$($counts["Invalid"]); path=$($Summary.Path)"
    }
    $observedCount = [long] $counts["Passed"] +
        [long] $counts["Failed"] +
        [long] $counts["Skipped"] +
        [long] $counts["Inconclusive"] +
        [long] $counts["Invalid"]
    if ($observedCount -ne [long] $counts["Total"]) {
        $countMessage = "{0} has inconsistent test counts: passed={1}; failed={2}; skipped={3}; " +
            "inconclusive={4}; invalid={5}; total={6}; path={7}"
        throw ($countMessage -f `
            $Context, $counts["Passed"], $counts["Failed"], $counts["Skipped"],
            $counts["Inconclusive"], $counts["Invalid"], $counts["Total"], $Summary.Path)
    }
    if ($counts["Total"] -eq 0) {
        throw "$Context has no tests: total=0; path=$($Summary.Path)"
    }
    if ($counts["Total"] -lt $MinimumTotal) {
        throw "$Context has too few tests: total=$($counts["Total"]); minimumTotal=$MinimumTotal; path=$($Summary.Path)"
    }
    if ($counts["Passed"] -lt $MinimumPassed) {
        throw "$Context has too few passed tests: passed=$($counts["Passed"]); minimumPassed=$MinimumPassed; path=$($Summary.Path)"
    }

    $allowedSkipSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($allowedSkip in @($AllowedSkippedTests)) {
        if ([string]::IsNullOrWhiteSpace($allowedSkip)) {
            throw "$Context allowed skip policy contains an empty test name"
        }
        [void] $allowedSkipSet.Add([string] $allowedSkip)
    }

    $skippedTestNamesProperty = $Summary.PSObject.Properties["SkippedTestNames"]
    if ($null -eq $skippedTestNamesProperty) {
        throw "$Context cannot verify skipped-test evidence because the summary has no skipped test names: skipped=$($counts["Skipped"]); path=$($Summary.Path)"
    }

    $skippedTestNames = @($Summary.SkippedTestNames)
    if ($skippedTestNames.Count -ne $counts["Skipped"]) {
        throw "$Context skipped-test evidence is incomplete: skipped=$($counts["Skipped"]); named=$($skippedTestNames.Count); path=$($Summary.Path)"
    }

    if ($counts["Skipped"] -gt 0) {
        $unexpectedSkippedTests = @(
            $skippedTestNames | Where-Object {
                [string]::IsNullOrWhiteSpace([string] $_) -or -not $allowedSkipSet.Contains([string] $_)
            }
        )
        if ($unexpectedSkippedTests.Count -gt 0) {
            throw "$Context has unexpected skipped tests: $($unexpectedSkippedTests -join ', '); path=$($Summary.Path)"
        }
    }

    $isSkippedResult = [string] $Summary.Result -eq "Skipped" -or
        [string] $Summary.Result -eq "Skipped:Ignored"
    if ($isSkippedResult -and $counts["Skipped"] -eq 0) {
        throw "$Context has a skipped result without skipped tests: result=$($Summary.Result); path=$($Summary.Path)"
    }
    if ($counts["Failed"] -gt 0) {
        throw "$Context contains failed tests: failed=$($counts["Failed"]); path=$($Summary.Path)"
    }
    if (-not $isSkippedResult -and [string] $Summary.Result -ne "Passed") {
        throw "$Context did not pass: result=$($Summary.Result); path=$($Summary.Path)"
    }

    return $Summary
}
