param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [string]$ConfigFile=(Join-Path $PSScriptRoot 'worker-pool.example.json'),
    [ValidateRange(0,86400)][int]$DurationSeconds=0
)
# PowerShell 7.4+. No installation, scheduled task, or system-wide process termination.
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion -lt [Version]'7.4'){throw 'PowerShell 7.4 or later is required.'}
$binary=(Resolve-Path -LiteralPath $Executable).Path
$settings=Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json
if($settings.maxWorkers -lt 1 -or $settings.maxWorkers -gt 16){throw 'maxWorkers must be 1..16'}
if($settings.port -lt 1 -or $settings.port -gt 65535 -or $settings.scheme -notin @('http','https')){throw 'Invalid server endpoint'}
if([string]::IsNullOrWhiteSpace($settings.host) -or [string]::IsNullOrWhiteSpace($settings.serverKey)){throw 'Missing host or serverKey'}
$secret=$env:OBJECT_HEAD_WORKER_KEY
if([string]::IsNullOrWhiteSpace($secret) -or $secret.Length -lt 32){throw 'Set OBJECT_HEAD_WORKER_KEY to the matching Nakama runtime secret (32+ characters).'}
$logs=Join-Path $PSScriptRoot 'worker-logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$slots=@(for($i=0;$i -lt $settings.maxWorkers;$i++){
    [pscustomobject]@{Index=$i;Process=$null;NextStart=[DateTime]::UtcNow;Started=[DateTime]::UtcNow;Failures=0}
})
$began=[DateTime]::UtcNow
try{
    while($DurationSeconds -eq 0 -or ([DateTime]::UtcNow-$began).TotalSeconds -lt $DurationSeconds){
        foreach($slot in $slots){
            if($null -ne $slot.Process -and $slot.Process.HasExited){
                $slot.Process.WaitForExit()
                $elapsed=([DateTime]::UtcNow-$slot.Started).TotalSeconds
                $slot.Failures=if($elapsed -lt 20){[Math]::Min($slot.Failures+1,5)}else{0}
                $delay=if($slot.Failures -gt 0){[Math]::Min(30,[Math]::Pow(2,$slot.Failures))}else{1}
                Write-Output "Worker $($slot.Index) exited ($($slot.Process.ExitCode)); restart in $delay seconds."
                $slot.Process.Dispose();$slot.Process=$null;$slot.NextStart=[DateTime]::UtcNow.AddSeconds($delay)
            }
            if($null -eq $slot.Process -and [DateTime]::UtcNow -ge $slot.NextStart){
                $log=Join-Path $logs ('worker-'+$slot.Index+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.log')
                $options=@('-batchmode','-nographics','-objectHeadWorker','-logFile',('"'+$log+'"'))
                $slot.Process=Start-Process -FilePath $binary -ArgumentList $options -WindowStyle Hidden -PassThru -Environment @{
                    OBJECT_HEAD_SERVER_SCHEME=[string]$settings.scheme;OBJECT_HEAD_SERVER_HOST=[string]$settings.host;
                    OBJECT_HEAD_SERVER_PORT=[string]$settings.port;OBJECT_HEAD_SERVER_KEY=[string]$settings.serverKey;
                    OBJECT_HEAD_WORKER_KEY=$secret
                }
                $slot.Started=[DateTime]::UtcNow
                Write-Output "Worker $($slot.Index) started (PID $($slot.Process.Id))."
            }
        }
        Start-Sleep -Milliseconds 500
    }
}finally{
    foreach($slot in $slots){
        if($null -ne $slot.Process){
            if(!$slot.Process.HasExited){$slot.Process.Kill();$slot.Process.WaitForExit()}
            $slot.Process.Dispose()
        }
    }
    Write-Output 'All workers owned by this pool have been closed.'
}
