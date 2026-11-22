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

# Skipped-test allowlists live in one tracked data file next to this script. Each gate
# (CI release evidence, local unity-tests.ps1, LTS compatibility gate) names its entries
# there; this is the only place that interprets exact, prefix, and explicit matches.
$script:NUnitSkipAllowlistDefaultPath = Join-Path $PSScriptRoot "unity-skip-allowlist.json"

function Get-NUnitSkipAllowlist {
    <#
    .SYNOPSIS
        Loads the skipped-test allowlist of one gate from unity-skip-allowlist.json.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string] $Gate,
        [string] $Path = $script:NUnitSkipAllowlistDefaultPath
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Skipped-test allowlist was not found: $Path"
    }

    try {
        $document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw "Skipped-test allowlist is malformed: $Path ($($_.Exception.Message))"
    }

    if ([string] $document.schemaVersion -ne "1") {
        throw "Skipped-test allowlist has an unsupported schemaVersion '$($document.schemaVersion)': $Path"
    }

    $gateProperty = $null
    if ($null -ne $document.gates) {
        $gateProperty = $document.gates.PSObject.Properties[$Gate]
    }
    if ($null -eq $gateProperty) {
        throw "Skipped-test allowlist has no gate '$Gate': $Path"
    }

    $explicitPolicy = [string] $gateProperty.Value.explicitPolicy
    if (@("listed", "any", "none") -notcontains $explicitPolicy) {
        throw "Skipped-test allowlist gate '$Gate' has an invalid explicitPolicy '$explicitPolicy': $Path"
    }

    $exact = New-Object System.Collections.Generic.List[string]
    $prefixes = New-Object System.Collections.Generic.List[string]
    $explicit = New-Object System.Collections.Generic.List[string]
    foreach ($test in @($document.tests)) {
        $name = [string] $test.name
        if ([string]::IsNullOrWhiteSpace($name)) {
            throw "Skipped-test allowlist contains an entry without a name: $Path"
        }
        if ($null -eq $test.match) {
            throw "Skipped-test allowlist entry '$name' has no match: $Path"
        }

        $matchProperty = $test.match.PSObject.Properties[$Gate]
        if ($null -eq $matchProperty) {
            continue
        }

        switch -CaseSensitive ([string] $matchProperty.Value) {
            "exact" { $exact.Add($name) }
            "prefix" { $prefixes.Add($name) }
            "explicit" { $explicit.Add($name) }
            default {
                throw "Skipped-test allowlist entry '$name' has an invalid match '$($matchProperty.Value)' for gate '$Gate': $Path"
            }
        }
    }

    [pscustomobject] @{
        Path = $Path
        Gate = $Gate
        ExplicitPolicy = $explicitPolicy
        Exact = $exact.ToArray()
        Prefixes = $prefixes.ToArray()
        Explicit = $explicit.ToArray()
    }
}

function Test-NUnitSkipPrefixMatch {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyString()][string] $Name,
        [Parameter(Mandatory = $true)][string] $Prefix
    )

    # A prefix admits the test itself and its parameterized cases, nothing broader.
    return $Name.Equals($Prefix, [System.StringComparison]::Ordinal) -or
        $Name.StartsWith($Prefix + "(", [System.StringComparison]::Ordinal)
}

function Get-NUnitAllowedSkippedTests {
    <#
    .SYNOPSIS
        Returns the skipped test names a gate admits for a parsed NUnit summary.

    .DESCRIPTION
        The result is passed to Assert-NUnitTestRunEvidence -AllowedSkippedTests, which
        still requires every skipped test to be named. Exact entries are admitted as is,
        prefix entries admit the skipped tests they match, and [Explicit] skips are
        admitted according to the gate's explicitPolicy. With 'listed', an [Explicit]
        skip that is not listed fails here.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][psobject] $Summary,
        [Parameter(Mandatory = $true)][string] $Gate,
        [string] $AllowlistPath = $script:NUnitSkipAllowlistDefaultPath,
        [string] $Context = "NUnit test evidence"
    )

    $allowlist = Get-NUnitSkipAllowlist -Gate $Gate -Path $AllowlistPath
    $allowed = New-Object System.Collections.Generic.List[string]
    foreach ($name in $allowlist.Exact) {
        $allowed.Add($name)
    }

    $explicitSkippedTestNames = @($Summary.ExplicitSkippedTestNames | Where-Object { $null -ne $_ })
    if ($allowlist.ExplicitPolicy -eq "listed") {
        $unapprovedExplicitSkippedTests = @(
            $explicitSkippedTestNames |
                Where-Object { $allowlist.Explicit -notcontains [string] $_ }
        )
        if ($unapprovedExplicitSkippedTests.Count -gt 0) {
            throw "$Context has unapproved explicit skipped tests: $($unapprovedExplicitSkippedTests -join ', '); path=$($Summary.Path)"
        }
    }
    if ($allowlist.ExplicitPolicy -eq "listed" -or $allowlist.ExplicitPolicy -eq "any") {
        foreach ($name in $explicitSkippedTestNames) {
            $allowed.Add([string] $name)
        }
    }

    if ($allowlist.Prefixes.Count -gt 0) {
        foreach ($skippedTestName in @($Summary.SkippedTestNames)) {
            foreach ($prefix in $allowlist.Prefixes) {
                if (Test-NUnitSkipPrefixMatch -Name ([string] $skippedTestName) -Prefix $prefix) {
                    $allowed.Add([string] $skippedTestName)
                    break
                }
            }
        }
    }

    return ,$allowed.ToArray()
}
