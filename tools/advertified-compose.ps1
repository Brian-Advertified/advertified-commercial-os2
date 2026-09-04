$script:AdvertifiedComposeProject = 'advertified-os2-dev'

function Assert-AdvertifiedComposeProject {
    param([switch]$RequireExisting)

    $labelRows = @(
        & docker ps -a `
            --filter 'label=com.docker.compose.project' `
            --format '{{.Names}}|{{.Labels}}'
    )
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect Docker Compose projects.'
    }
    $records = @(
        $labelRows | ForEach-Object {
            $parts = $_ -split '\|', 2
            if ($parts.Count -ne 2) { return }
            $labels = @{}
            foreach ($label in ($parts[1] -split ',')) {
                $pair = $label -split '=', 2
                if ($pair.Count -eq 2) {
                    $labels[$pair[0]] = $pair[1]
                }
            }
            $project = $labels['com.docker.compose.project']
            if ($project) {
                [pscustomobject]@{
                    Name = $parts[0]
                    'com.docker.compose.project' = $project
                    'com.docker.compose.service' =
                        $labels['com.docker.compose.service']
                    'com.docker.compose.oneoff' =
                        $labels['com.docker.compose.oneoff']
                }
            }
        }
    )
    $records = @(
        $records |
            Group-Object -Property Name |
            ForEach-Object { $_.Group[0] }
    )
    $projects = $records | ForEach-Object {
        $_.'com.docker.compose.project'
    }

    $foreign = @(
        $projects |
            Where-Object {
                $_ -like 'advertified*' -and
                $_ -ne $script:AdvertifiedComposeProject
            } |
            Sort-Object -Unique
    )
    if ($foreign.Count -gt 0) {
        throw (
            'Refusing to create a second Advertified stack. ' +
            "Only '$script:AdvertifiedComposeProject' is allowed; found: " +
            ($foreign -join ', ')
        )
    }
    if ($RequireExisting -and $script:AdvertifiedComposeProject -notin $projects) {
        throw "The existing '$script:AdvertifiedComposeProject' stack was not found."
    }

    $duplicateGroups = @(
        $records |
            Where-Object {
                $_.'com.docker.compose.project' -eq $script:AdvertifiedComposeProject -and
                $_.'com.docker.compose.oneoff' -ne 'True' -and
                $_.Name -like 'advertified-os2-dev-*'
            } |
            Group-Object -Property 'com.docker.compose.service' |
            Where-Object Count -gt 1
    )
    if ($duplicateGroups.Count -gt 0) {
        $details = $duplicateGroups | ForEach-Object {
            $_.Name + ': ' + (($_.Group | ForEach-Object Name) -join ', ')
        }
        throw "Duplicate advertified-os2-dev services found: $($details -join '; ')"
    }
}

function Invoke-AdvertifiedCompose {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ComposeFiles,
        [Parameter(Mandatory = $true)]
        [string[]]$ComposeArguments
    )

    Assert-AdvertifiedComposeProject -RequireExisting
    $arguments = @('compose', '--project-name', $script:AdvertifiedComposeProject)
    foreach ($file in $ComposeFiles) {
        $arguments += @('--file', $file)
    }
    $arguments += $ComposeArguments
    & docker @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose failed: $($ComposeArguments -join ' ')"
    }
}

function Get-AdvertifiedServiceContainer {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ComposeFiles,
        [Parameter(Mandatory = $true)]
        [string]$Service
    )

    $container = @(
        Invoke-AdvertifiedCompose `
            -ComposeFiles $ComposeFiles `
            -ComposeArguments @('ps', '--all', '--quiet', $Service)
    )
    if ($container.Count -ne 1 -or -not $container[0]) {
        throw "Expected exactly one '$Service' container in advertified-os2-dev."
    }
    return $container[0]
}

function Wait-AdvertifiedService {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ComposeFiles,
        [Parameter(Mandatory = $true)]
        [string]$Service
    )

    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $container = Get-AdvertifiedServiceContainer $ComposeFiles $Service
        $state = & docker inspect --format `
            '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' `
            $container 2>$null
        if ($LASTEXITCODE -eq 0 -and $state -in @('healthy', 'running')) {
            return
        }
        Start-Sleep -Seconds 1
    }

    Invoke-AdvertifiedCompose $ComposeFiles @('logs', '--tail', '80', $Service)
    throw "The advertified-os2-dev '$Service' service did not become healthy."
}
