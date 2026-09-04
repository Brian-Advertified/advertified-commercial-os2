$script:AdvertifiedComposeProject = 'advertified-os2-dev'

function Assert-AdvertifiedComposeProject {
    param([switch]$RequireExisting)

    $containerIds = @(
        & docker ps -aq --filter 'label=com.docker.compose.project'
    )
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect Docker Compose projects.'
    }
    $records = @()
    if ($containerIds.Count -gt 0) {
        $labelSets = & docker inspect `
            --format '{{json .Config.Labels}}' $containerIds
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to inspect Docker Compose labels.'
        }
        $records = @($labelSets | ForEach-Object { $_ | ConvertFrom-Json })
    }
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

    $duplicates = @(
        $records |
            Where-Object {
                $_.'com.docker.compose.project' -eq $script:AdvertifiedComposeProject
            } |
            Group-Object -Property 'com.docker.compose.service' |
            Where-Object Count -gt 1 |
            ForEach-Object Name
    )
    if ($duplicates.Count -gt 0) {
        throw "Duplicate advertified-os2-dev services found: $($duplicates -join ', ')"
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
