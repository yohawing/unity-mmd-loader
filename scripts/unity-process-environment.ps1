function Initialize-UnityProcessEnvironment {
    $commonApplicationData = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::CommonApplicationData)
    if ([string]::IsNullOrWhiteSpace($commonApplicationData)) {
        $commonApplicationData = "C:\ProgramData"
    }

    if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) {
        $env:ALLUSERSPROFILE = $commonApplicationData
    }
    if ([string]::IsNullOrWhiteSpace($env:ProgramData)) {
        $env:ProgramData = $commonApplicationData
    }
}
