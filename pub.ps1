# Publish-All-StartJob.ps1
$commands = @(
  'dotnet publish UFS2Tool.sln --configuration Release --runtime linux-x64 --self-contained --output "./Release/linux-x64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime osx-x64 --self-contained -p:UseAppHost=true --output "./Release/osx-x64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime win-x64 --self-contained --output "./Release/win-x64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime linux-arm64 --self-contained --output "./Release/linux-arm64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime osx-arm64 --self-contained -p:UseAppHost=true --output "./Release/osx-arm64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime win-arm64 --self-contained --output "./Release/win-arm64-selfcontained"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime linux-x64 --output "./Release/linux-x64"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime osx-x64 -p:UseAppHost=true --output "./Release/osx-x64"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime win-x64 --output "./Release/win-x64"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime linux-arm64 --output "./Release/linux-arm64"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime osx-arm64 -p:UseAppHost=true --output "./Release/osx-arm64"',
  'dotnet publish UFS2Tool.sln --configuration Release --runtime win-arm64 --output "./Release/win-arm64"'
)

$jobs = @()
foreach ($cmd in $commands) {
  Write-Host "Starting job for: $cmd"
  $stdoutFile = [System.IO.Path]::GetTempFileName()
  $stderrFile = [System.IO.Path]::GetTempFileName()

  $script = {
    param($command, $outFile, $errFile)
    # Run the command and redirect output to files
    if (Get-Command pwsh -ErrorAction SilentlyContinue) {
      & pwsh -NoProfile -Command $command 1> $outFile 2> $errFile
      $exit = $LASTEXITCODE
    } else {
      cmd /c $command 1> $outFile 2> $errFile
      $exit = $LASTEXITCODE
    }

    $out = if (Test-Path $outFile) { Get-Content $outFile -Raw } else { '' }
    $err = if (Test-Path $errFile) { Get-Content $errFile -Raw } else { '' }

    [pscustomobject]@{
      Command = $command
      ExitCode = $exit
      StdOut = $out
      StdErr = $err
      StdOutFile = $outFile
      StdErrFile = $errFile
    }
  }

  $jobs += Start-Job -ScriptBlock $script -ArgumentList $cmd, $stdoutFile, $stderrFile
}

Write-Host "Waiting for jobs to complete..."
Wait-Job -Job $jobs

# Collect results (Receive-Job returns deserialized objects)
$results = $jobs | Receive-Job

# Normalize and print summary safely
$failed = $false
foreach ($r in $results) {
  # Ensure StdErr and StdOut are strings (handles arrays/deserialized objects)
  $stderrText = if ($null -ne $r.StdErr) { -join ($r.StdErr) } else { '' }
  $stdoutText = if ($null -ne $r.StdOut) { -join ($r.StdOut) } else { '' }
  $stderrText = [string]$stderrText
  $stdoutText = [string]$stdoutText

  if ($r.ExitCode -eq 0) {
    Write-Host "SUCCESS:" -ForegroundColor Green
    Write-Host "  $($r.Command)"
  } else {
    $failed = $true
    Write-Host "FAILED:" -ForegroundColor Red
    Write-Host "  $($r.Command)"
    Write-Host "  ExitCode: $($r.ExitCode)"
    if ($stderrText.Length -gt 0) {
      $preview = $stderrText.Substring(0, [Math]::Min(200, $stderrText.Length))
      Write-Host "  StdErr (first 200 chars):"
      Write-Host "  $preview"
    } else {
      Write-Host "  StdErr: (empty)"
    }
    Write-Host "  StdOut file: $($r.StdOutFile)"
    Write-Host "  StdErr file: $($r.StdErrFile)"
  }
}

# Clean up jobs
$jobs | Remove-Job -Force

if ($failed) {
  Write-Host "One or more publishes failed." -ForegroundColor Red
  exit 1
}

Write-Host "All publishes finished successfully." -ForegroundColor Green
exit 0
