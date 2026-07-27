<#
.SYNOPSIS
    Phase 0 of the mission-system plan: move the loose gameplay scripts in Assets/
    into the Assets/Scripts/ taxonomy that AGENTS.md mandates.

.DESCRIPTION
    Each .cs is moved together with its .cs.meta. That pairing is the whole point:
    a .cs that arrives without its .meta gets a fresh GUID from Unity, and every
    component reference in SampleScene.unity (3 MB of YAML) becomes
    "Missing (Mono Script)".

    GUIDs are path-independent, so when both files move together the scene needs
    no edits at all. `git diff --stat` afterwards should show renames only, and
    Assets/Scenes/SampleScene.unity must NOT appear.

    Doing the same drag inside the Unity Editor's Project window is equally safe
    and is the recommended route if the Editor is already open.

.NOTES
    Unity MUST be closed. The script refuses to run otherwise.

.EXAMPLE
    pwsh -File tools/move-scripts-to-Scripts.ps1 -WhatIf
    pwsh -File tools/move-scripts-to-Scripts.ps1
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- preflight

if (Get-Process -Name Unity -ErrorAction SilentlyContinue) {
    throw "Unity is running. Close the Editor first — moving .cs files underneath a live Editor can race the .meta and reassign GUIDs."
}

Push-Location $ProjectRoot
try {
    $status = git status --porcelain
    if ($status -and -not $WhatIfPreference) {
        Write-Warning "Working tree is not clean:"
        $status | ForEach-Object { Write-Warning "  $_" }
        throw "Commit or stash first. This move should be its own commit so the rename diff is reviewable."
    }

    # ------------------------------------------------------------ move table

    $moves = [ordered]@{
        'Assets/PlayerController.cs'             = 'Assets/Scripts/Player/PlayerController.cs'
        'Assets/Audio/Footsteps/footstepaudio.cs' = 'Assets/Scripts/Player/footstepaudio.cs'
        'Assets/CameraController.cs'             = 'Assets/Scripts/CameraRig/CameraController.cs'
        'Assets/CameraCollision.cs'              = 'Assets/Scripts/CameraRig/CameraCollision.cs'
        'Assets/doorController.cs'               = 'Assets/Scripts/Interaction/doorController.cs'
        'Assets/DoorSideDetector.cs'             = 'Assets/Scripts/Interaction/DoorSideDetector.cs'
        'Assets/NPCInteraction.cs'               = 'Assets/Scripts/Interaction/NPCInteraction.cs'
        'Assets/GroqDialogue.cs'                 = 'Assets/Scripts/Dialogue/GroqDialogue.cs'
        'Assets/ElevenLabsTTS.cs'                = 'Assets/Scripts/Dialogue/ElevenLabsTTS.cs'
        'Assets/CarAI.cs'                        = 'Assets/Scripts/Vehicles/CarAI.cs'
        'Assets/Audio/Car Sound/soundController.cs' = 'Assets/Scripts/Vehicles/soundController.cs'
        'Assets/MapToggle.cs'                    = 'Assets/Scripts/UI/MapToggle.cs'
        'Assets/MiniMapFollow.cs'                = 'Assets/Scripts/UI/MiniMapFollow.cs'
        'Assets/MiniMapArrow.cs'                 = 'Assets/Scripts/UI/MiniMapArrow.cs'
    }

    # Assets/TutorialInfo/** is deliberately NOT moved: ReadmeEditor.cs must stay
    # under a folder literally named "Editor" or the player build breaks. That
    # folder is unused Unity template boilerplate and is better deleted outright,
    # in its own commit.

    $moved = 0

    foreach ($from in $moves.Keys) {
        $to = $moves[$from]

        $fromFull = Join-Path $ProjectRoot $from
        $toFull   = Join-Path $ProjectRoot $to

        if (-not (Test-Path -LiteralPath $fromFull)) {
            Write-Host "skip   $from (already moved or missing)" -ForegroundColor DarkGray
            continue
        }

        if (-not (Test-Path -LiteralPath "$fromFull.meta")) {
            throw "No .meta beside '$from'. Aborting — moving the .cs alone would break every scene reference to it."
        }

        $destDir = Split-Path $toFull -Parent
        if (-not (Test-Path -LiteralPath $destDir)) {
            if ($PSCmdlet.ShouldProcess($destDir, 'create directory')) {
                New-Item -ItemType Directory -Force -Path $destDir | Out-Null
            }
        }

        if ($PSCmdlet.ShouldProcess("$from -> $to", 'git mv (with .meta)')) {
            git mv --  $from       $to
            git mv --  "$from.meta" "$to.meta"
            $moved++
        }

        Write-Host "move   $from" -ForegroundColor Green
        Write-Host "    -> $to"   -ForegroundColor DarkGreen
    }

    Write-Host ""
    Write-Host "Moved $moved script(s)." -ForegroundColor Cyan

    if (-not $WhatIfPreference) {
        Write-Host ""
        Write-Host "Diff summary (expect renames only, and NO SampleScene.unity):" -ForegroundColor Cyan
        git diff --cached --stat

        if (git diff --cached --name-only | Select-String -Quiet 'Scenes/SampleScene.unity') {
            Write-Warning "SampleScene.unity is in the diff. That should not happen — review before committing."
        }

        Write-Host ""
        Write-Host "Next: reopen Unity, let it reimport, then verify in Play mode:" -ForegroundColor Yellow
        Write-Host "  walk / run / jump, camera, minimap, M map, school door, both Groq NPCs, car traffic."
        Write-Host "  No component should show 'Missing (Mono Script)'."
    }
}
finally {
    Pop-Location
}
