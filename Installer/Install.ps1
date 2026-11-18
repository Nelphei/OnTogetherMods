<#
.SYNOPSIS
    Installs r2modman and every SoapieScripts mod for On-Together.

.DESCRIPTION
    1. Installs r2modman (the mod manager) if it isn't installed yet.
    2. Sets up an r2modman profile for On-Together called "SoapieScripts", with BepInEx
       and the newest version of every SoapieScripts mod on Thunderstore.

    Run it again any time to update the mods. Other mods in the profile, your mod
    settings, and mods you switched off in r2modman are left as they are.

    Quickest way to run it (paste into PowerShell):
        irm https://raw.githubusercontent.com/Nelphei/OnTogetherMods/master/Installer/Install.ps1 | iex
    Or double-click Install.cmd next to this file.

.PARAMETER ProfileName
    The r2modman profile to set up. Default: SoapieScripts.
.PARAMETER Skip
    Mods to leave out, for example: -Skip "Desk Swarm", "Quick Hide"
.PARAMETER NoR2modman
    Don't install r2modman, only set up the profile.
.PARAMETER NoLaunch
    Don't open r2modman at the end.
.PARAMETER DataFolder
    r2modman's data folder, if you changed it in r2modman's settings.
#>
param(
    [string]$ProfileName = 'SoapieScripts',
    [string[]]$Skip = @(),
    [switch]$NoR2modman,
    [switch]$NoLaunch,
    [string]$DataFolder = ''
)

$ErrorActionPreference = 'Stop'

$Team       = 'SoapieScripts'
$Community  = 'on-together'
$GameFolder = 'OnTogetherVirtualCoWorking'   # r2modman's folder name for On-Together
$SteamAppId = '3707400'
$LoaderName = 'BepInEx-BepInExPack'
$LoaderRoot = 'BepInExPack'                  # the folder in BepInExPack's zip that goes in the profile

# Where On-Together's install rules (Thunderstore's ecosystem schema, which r2modman follows)
# put a package's files. All but config get a subfolder per package.
$Routes = [ordered]@{
    plugins  = 'BepInEx\plugins'
    core     = 'BepInEx\core'
    patchers = 'BepInEx\patchers'
    monomod  = 'BepInEx\monomod'
    config   = 'BepInEx\config'
}

function Say([string]$Text, [string]$Color = 'Gray') { Write-Host $Text -ForegroundColor $Color }

function Get-Text([string]$Url) {
    $web = New-Object Net.WebClient
    try {
        $web.Headers['User-Agent'] = 'SoapieScripts-Installer'
        $web.Encoding = [Text.Encoding]::UTF8
        return $web.DownloadString($Url)
    } finally { $web.Dispose() }
}

function Get-File([string]$Url, [string]$Path) {
    $web = New-Object Net.WebClient
    try {
        $web.Headers['User-Agent'] = 'SoapieScripts-Installer'
        $web.DownloadFile($Url, $Path)
    } finally { $web.Dispose() }
}

# Windows PowerShell's ConvertFrom-Json has a size limit; this reader doesn't.
function Read-Json([string]$Text) {
    if ($PSVersionTable.PSVersion.Major -ge 6) { return ,($Text | ConvertFrom-Json) }
    Add-Type -AssemblyName System.Web.Extensions
    $reader = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $reader.MaxJsonLength = [int]::MaxValue
    return ,$reader.DeserializeObject($Text)
}

function Simple([string]$Text) { ($Text -replace '[^A-Za-z0-9]', '').ToLowerInvariant() }

function Show-Name($Package) {
    if ($Package.full_name -eq $LoaderName) { return 'BepInEx' }
    return ($Package.name -replace '_', ' ')
}

function Copy-Tree([string]$From, [string]$To) {
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $From -Force) {
        $target = Join-Path $To $item.Name
        if ($item.PSIsContainer) { Copy-Tree $item.FullName $target }
        else { Copy-Item -LiteralPath $item.FullName -Destination $target -Force }
    }
}

function Wait-Closed([string[]]$Names, [string]$What) {
    while (Get-Process -Name $Names -ErrorAction SilentlyContinue) {
        Say "$What is running. Close it, then press Enter." Yellow
        [void](Read-Host)
    }
}

# ---------- r2modman ----------

function Find-R2modman {
    $keys = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*',
            'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    foreach ($key in $keys) {
        foreach ($entry in Get-ItemProperty -Path $key -ErrorAction SilentlyContinue) {
            if ($entry.DisplayName -like 'r2modman*' -and $entry.InstallLocation) {
                $exe = Join-Path $entry.InstallLocation.Trim('"') 'r2modman.exe'
                if (Test-Path -LiteralPath $exe) { return $exe }
            }
        }
    }
    $exe = Join-Path $env:LOCALAPPDATA 'Programs\r2modman\r2modman.exe'
    if (Test-Path -LiteralPath $exe) { return $exe }
    return $null
}

function Install-R2modman([string]$Temp) {
    Say 'Finding the newest r2modman on GitHub...'
    $release = Read-Json (Get-Text 'https://api.github.com/repos/ebkr/r2modmanPlus/releases/latest')
    $asset = @($release.assets) | Where-Object { $_.name -match '^r2modman-Setup-[\d.]+\.exe$' } | Select-Object -First 1
    if (-not $asset) { throw "Couldn't find r2modman's installer in its newest GitHub release." }

    $setup = Join-Path $Temp $asset.name
    Say ('Downloading {0} ({1:N0} MB)...' -f $asset.name, ($asset.size / 1MB))
    Get-File $asset.browser_download_url $setup
    if ("$($asset.digest)" -match '^sha256:([0-9a-fA-F]{64})$') {
        if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $Matches[1]) {
            throw 'The r2modman download came out damaged (its checksum is wrong). Run this again.'
        }
    }

    Say 'Installing r2modman...'
    $process = Start-Process -FilePath $setup -ArgumentList '/S' -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "r2modman's installer stopped with code $($process.ExitCode)." }
    $exe = Find-R2modman
    if (-not $exe) { throw "r2modman's installer finished, but r2modman.exe isn't where it should be." }
    return $exe
}

# ---------- The game ----------

function Find-Game {
    $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $steam) { return $null }
    $libraries = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"]+)"')) {
            $libraries += $m.Groups[1].Value.Replace('\\', '\')
        }
    }
    foreach ($library in $libraries) {
        $acf = Join-Path $library "steamapps\appmanifest_$SteamAppId.acf"
        if (Test-Path -LiteralPath $acf) {
            $folder = 'On-Together'
            if ([IO.File]::ReadAllText($acf) -match '"installdir"\s+"([^"]+)"') { $folder = $Matches[1] }
            return (Join-Path $library "steamapps\common\$folder")
        }
    }
    return $null
}

# ---------- The profile's mod list (mods.yml) ----------

# r2modman writes mods.yml as a YAML list, one block per mod starting with "- ".
# Blocks are kept as text, so mods this script doesn't install are written back untouched.
function Read-ModList([string]$Path) {
    $entries = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $Path)) { return ,$entries }
    $text = [IO.File]::ReadAllText($Path)
    if ($text.Trim() -eq '' -or $text.Trim() -eq '[]') { return ,$entries }

    $current = $null
    foreach ($line in ($text -split "`r?`n")) {
        if ($line.StartsWith('- ')) {
            $current = [pscustomobject]@{ Name = ''; Version = ''; Enabled = $true; Lines = New-Object System.Collections.ArrayList }
            [void]$entries.Add($current)
        }
        if ($current) { [void]$current.Lines.Add($line) }
        elseif ($line.Trim() -ne '') { throw "Couldn't read $Path, so nothing was changed." }
    }

    foreach ($entry in $entries) {
        while ($entry.Lines.Count -gt 0 -and $entry.Lines[$entry.Lines.Count - 1].Trim() -eq '') {
            $entry.Lines.RemoveAt($entry.Lines.Count - 1)
        }
        $version = @{}
        foreach ($line in $entry.Lines) {
            if ($line -match '^(- |  )name: [''"]?([^''"\s]+)') { $entry.Name = $Matches[2] }
            elseif ($line -match '^(- |  )enabled: (true|false)') { $entry.Enabled = ($Matches[2] -eq 'true') }
            elseif ($line -match '^    (major|minor|patch): (\d+)') { $version[$Matches[1]] = $Matches[2] }
        }
        if ($version.Count -eq 3) { $entry.Version = "$($version.major).$($version.minor).$($version.patch)" }
        if (-not $entry.Name) { throw "Couldn't read $Path, so nothing was changed." }
    }
    return ,$entries
}

function Quote([string]$Text) { ConvertTo-Json $Text -Compress }

# The same fields r2modman saves for a mod it installed from Thunderstore.
function New-Entry($Package, $Version, [bool]$Enabled) {
    $parts = $Version.version_number.Split('.')
    $lines = New-Object System.Collections.ArrayList
    [void]$lines.AddRange(@(
        '- manifestVersion: 1'
        "  name: $(Quote $Package.full_name)"
        "  authorName: $(Quote $Package.owner)"
        "  websiteUrl: $(Quote $Package.package_url)"
        "  displayName: $(Quote $Package.name)"
        "  description: $(Quote $Version.description)"
        "  gameVersion: '0'"
        '  networkMode: both'
        '  packageType: other'
        '  installMode: managed'
        "  installedAtTime: $([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
        '  loaders: []'
    ))
    $dependencies = @($Version.dependencies)
    if ($dependencies.Count -eq 0) { [void]$lines.Add('  dependencies: []') }
    else {
        [void]$lines.Add('  dependencies:')
        foreach ($d in $dependencies) { [void]$lines.Add("    - $(Quote $d)") }
    }
    [void]$lines.AddRange(@(
        '  incompatibilities: []'
        '  optionalDependencies: []'
        '  versionNumber:'
        "    major: $($parts[0])"
        "    minor: $($parts[1])"
        "    patch: $($parts[2])"
        "  enabled: $(if ($Enabled) { 'true' } else { 'false' })"
        '  onlineSource: false'
        '  trustedPackage: false'
    ))
    return [pscustomobject]@{ Name = $Package.full_name; Version = $Version.version_number; Enabled = $Enabled; Lines = $lines }
}

# ---------- Installing a package ----------

function Get-Newest($Package) {
    @($Package.versions) | Where-Object { $_.is_active -ne $false } |
        Sort-Object { [version]$_.version_number } -Descending | Select-Object -First 1
}

function Test-Installed([string]$Name) {
    if ($Name -eq $LoaderName) { return (Test-Path -LiteralPath (Join-Path $ProfileDir 'BepInEx\core\BepInEx.Preloader.dll')) }
    foreach ($route in $Routes.Values) {
        if (Test-Path -LiteralPath (Join-Path $ProfileDir (Join-Path $route $Name))) { return $true }
    }
    return $false
}

# Puts a package's files where r2modman would: a known folder (plugins, config...) goes to its
# route, loose files go to plugins (.mm.dll to monomod), and other folders are looked inside.
function Install-ByRules([string]$From, [string]$Name) {
    foreach ($item in Get-ChildItem -LiteralPath $From -Force) {
        if ($item.PSIsContainer) {
            $key = $item.Name.ToLowerInvariant()
            if ($Routes.Contains($key)) {
                $to = Join-Path $ProfileDir $Routes[$key]
                if ($key -ne 'config') { $to = Join-Path $to $Name }
                Copy-Tree $item.FullName $to
            } else {
                Install-ByRules $item.FullName $Name
            }
        } else {
            $route = $Routes.plugins
            if ($item.Name -like '*.mm.dll') { $route = $Routes.monomod }
            $to = Join-Path $ProfileDir (Join-Path $route $Name)
            New-Item -ItemType Directory -Force -Path $to | Out-Null
            Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $to $item.Name) -Force
        }
    }
}

function Install-Package($Package, $Version, [bool]$Enabled, [string]$Temp) {
    $name = $Package.full_name

    # r2modman keeps every download unzipped in cache\<package>\<version>, and reuses it.
    $cache = Join-Path $CacheDir (Join-Path $name $Version.version_number)
    if (-not (Test-Path -LiteralPath (Join-Path $cache 'manifest.json'))) {
        $zip = Join-Path $Temp "$name.zip"
        $unzipped = Join-Path $Temp $name
        Get-File $Version.download_url $zip
        if (Test-Path -LiteralPath $unzipped) { Remove-Item -LiteralPath $unzipped -Recurse -Force }
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $unzipped)
        if (-not (Test-Path -LiteralPath (Join-Path $unzipped 'manifest.json'))) { throw "The download of $name has no manifest.json." }
        if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Recurse -Force }
        Copy-Tree $unzipped $cache
    }

    if ($name -eq $LoaderName) {
        # BepInEx itself: everything inside BepInExPack\ goes in the profile's top folder.
        foreach ($item in Get-ChildItem -LiteralPath (Join-Path $cache $LoaderRoot) -Force) {
            if (@('manifest.json', 'readme.md', 'icon.png') -contains $item.Name.ToLowerInvariant()) { continue }
            $to = Join-Path $ProfileDir $item.Name
            if ($item.PSIsContainer) { Copy-Tree $item.FullName $to }
            else { Copy-Item -LiteralPath $item.FullName -Destination $to -Force }
        }
        return
    }

    foreach ($route in $Routes.Values) {
        $old = Join-Path $ProfileDir (Join-Path $route $name)
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force }
    }
    Install-ByRules $cache $name

    # r2modman switches a mod off by adding .old to its files.
    if (-not $Enabled) {
        foreach ($route in $Routes.Values) {
            if ($route -eq $Routes.config) { continue }
            $folder = Join-Path $ProfileDir (Join-Path $route $name)
            if (Test-Path -LiteralPath $folder) {
                Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object { $_.Name -notlike '*.old' } |
                    ForEach-Object { Rename-Item -LiteralPath $_.FullName -NewName "$($_.Name).old" }
            }
        }
    }
}

# ---------- Main ----------

if ($env:OS -ne 'Windows_NT') {
    throw 'This installer is for Windows. On a Mac, use install-mac.sh (see the README).'
}

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $DataFolder) { $DataFolder = Join-Path $env:APPDATA 'r2modmanPlus-local' }
$ModRoot    = Join-Path $DataFolder $GameFolder
$CacheDir   = Join-Path $ModRoot 'cache'
$ProfileDir = Join-Path $ModRoot "profiles\$ProfileName"
$Temp       = Join-Path ([IO.Path]::GetTempPath()) ('SoapieScripts-install-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $Temp | Out-Null

try {
    Say ''
    Say 'SoapieScripts mods for On-Together' Cyan
    Say ''

    Wait-Closed @('OnTogether', 'On-Together') 'On-Together'
    Wait-Closed @('r2modman') 'r2modman'

    $exe = Find-R2modman
    if ($exe) { Say 'r2modman is already installed.' }
    elseif ($NoR2modman) { Say 'Skipping r2modman, as asked.' }
    else {
        $exe = Install-R2modman $Temp
        Say 'r2modman is installed.' Green
    }

    $game = Find-Game
    if (-not $game) {
        Say "Couldn't find On-Together in Steam. Install it from Steam before starting r2modman." Yellow
    }

    Say ''
    Say 'Getting the mod list from Thunderstore...'
    $listing = Read-Json (Get-Text "https://thunderstore.io/c/$Community/api/v1/package/")
    $byName = @{}
    foreach ($package in $listing) { $byName[$package.full_name] = $package }

    $skipNames = @($Skip | ForEach-Object { Simple $_ })
    $ours = @($listing | Where-Object { $_.owner -eq $Team -and -not $_.is_deprecated } | Sort-Object { $_.name })
    if ($ours.Count -eq 0) { throw "Thunderstore lists no $Team mods for On-Together right now." }

    # Install order: each package after the packages it needs, BepInEx first.
    $plan = New-Object System.Collections.Specialized.OrderedDictionary
    function Add-ToPlan($Package) {
        if ($plan.Contains($Package.full_name)) { return }
        $version = Get-Newest $Package
        foreach ($dependency in @($version.dependencies)) {
            $needed = $dependency -replace '-[^-]+$', ''
            if ($byName.ContainsKey($needed)) { Add-ToPlan $byName[$needed] }
            else { Say "  $(Show-Name $Package) needs $needed, which isn't on Thunderstore for On-Together." Yellow }
        }
        $plan[$Package.full_name] = $Package
    }
    if (-not $byName.ContainsKey($LoaderName)) { throw "Thunderstore doesn't list $LoaderName for On-Together." }
    Add-ToPlan $byName[$LoaderName]
    foreach ($package in $ours) {
        if ($skipNames -contains (Simple $package.name) -or $skipNames -contains (Simple $package.full_name)) {
            Say "  Leaving out $(Show-Name $package), as asked."
            continue
        }
        Add-ToPlan $package
    }

    New-Item -ItemType Directory -Force -Path $ProfileDir | Out-Null
    $modsYml = Join-Path $ProfileDir 'mods.yml'
    $entries = Read-ModList $modsYml

    Say ''
    Say "Mods in the r2modman profile '$ProfileName':"
    $changed = $false
    foreach ($package in $plan.Values) {
        $version = Get-Newest $package
        $label = '  {0} {1}' -f (Show-Name $package), $version.version_number
        $existing = $entries | Where-Object { $_.Name -eq $package.full_name } | Select-Object -First 1

        if ($existing -and $existing.Version -eq $version.version_number -and (Test-Installed $package.full_name)) {
            Say "$label  (already there)"
            continue
        }

        $enabled = $true
        if ($existing) { $enabled = $existing.Enabled }
        Install-Package $package $version $enabled $Temp
        $entry = New-Entry $package $version $enabled
        if ($existing) {
            $entries[$entries.IndexOf($existing)] = $entry
            $was = ''
            if ($existing.Version -and $existing.Version -ne $version.version_number) { $was = " (was $($existing.Version))" }
            Say "$label  updated$was" Green
        } else {
            [void]$entries.Add($entry)
            Say "$label  installed" Green
        }
        $changed = $true
    }

    if ($changed) {
        if (Test-Path -LiteralPath $modsYml) { Copy-Item -LiteralPath $modsYml -Destination "$modsYml.bak" -Force }
        $text = ($entries | ForEach-Object { $_.Lines -join "`n" }) -join "`n"
        [IO.File]::WriteAllText($modsYml, "$text`n", (New-Object Text.UTF8Encoding $false))
    }

    if ($game -and ((Test-Path -LiteralPath (Join-Path $game 'On-Together\BepInEx\core')) -or
                    (Test-Path -LiteralPath (Join-Path $game 'BepInEx\core')))) {
        Say ''
        Say 'Note: the game folder also has its own BepInEx (put there by hand). Starting the game from' Yellow
        Say "Steam loads that one; r2modman's Start modded loads the '$ProfileName' profile instead." Yellow
    }

    Say ''
    Say 'All done!' Green
    Say "In r2modman: pick On-Together, then the '$ProfileName' profile, then Start modded."
    Say 'Run this again any time to update the mods.'

    if ($exe -and -not $NoLaunch) { Start-Process -FilePath $exe }
}
finally {
    Remove-Item -LiteralPath $Temp -Recurse -Force -ErrorAction SilentlyContinue
}
