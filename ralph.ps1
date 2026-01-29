param(
    [string]$tool = "codex",
    [int]$iter = 1
)

if ($tool -ne "codex") {
    Write-Host "Only codex tool is supported in this repo."
    exit 1
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
for ($i = 0; $i -le $iter - 1; $i++) {
    Get-Content -Raw "$scriptDir\CODEX.md" | & codex exec --dangerously-bypass-approvals-and-sandbox -C $scriptDir -
}
