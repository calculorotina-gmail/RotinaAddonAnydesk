# build.ps1 - Automated Build, Test, Publish and Installer Generation Script
$ErrorActionPreference = "Stop"

$logFile = Join-Path $PSScriptRoot "build.txt"
if (Test-Path $logFile) { Remove-Item $logFile -Force }

function Write-BuildLog {
    param([string]$message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $formatted = "[$timestamp] $message"
    Write-Host $formatted
    Out-File -FilePath $logFile -InputObject $formatted -Append -Encoding utf8
}

function Convert-PngToIco {
    param([string]$pngPath, [string]$icoPath)
    if (Test-Path $pngPath) {
        $pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
        $pngLen = $pngBytes.Length

        # Read dimensions from PNG IHDR chunk if present
        $w = 0
        $h = 0
        if ($pngBytes.Length -gt 24) {
            $w = $pngBytes[19]
            $h = $pngBytes[23]
        }

        $icoHeader = [byte[]]@(0, 0, 1, 0, 1, 0)
        $icoDir = [byte[]]@($w, $h, 0, 0, 1, 0, 32, 0)
        $sizeBytes = [System.BitConverter]::GetBytes([uint32]$pngLen)
        $offsetBytes = [System.BitConverter]::GetBytes([uint32]22)

        $fullIco = $icoHeader + $icoDir + $sizeBytes + $offsetBytes + $pngBytes
        [System.IO.File]::WriteAllBytes($icoPath, $fullIco)
        return $true
    }
    return $false
}

Write-BuildLog "========================================================="
Write-BuildLog "  AnyDeskMonitor / RotinaAddonAnydesk BUILD AUTOMATION"
Write-BuildLog "========================================================="

$startTime = Get-Date

try {
    # Step 1: Clean build directories
    Write-BuildLog "Etapa 1/12: A limpar compilações e serviços anteriores..."
    Start-Process "sc.exe" -ArgumentList "stop RotinaAddonAnydesk" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
    Start-Process "taskkill.exe" -ArgumentList "/F /T /IM RotinaAddonAnydesk.exe" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
    Start-Process "taskkill.exe" -ArgumentList "/F /T /IM RotinaAddonAnydesk-Setup.exe" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
    Start-Process "taskkill.exe" -ArgumentList "/F /T /IM aplicativo-Setup.exe" -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
    Get-Process -Name "RotinaAddonAnydesk", "RotinaAddonAnydesk-Setup", "aplicativo-Setup", "AnyDeskMonitor.Agent", "AnyDeskMonitor.Api", "AnyDeskMonitor.Web" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    if (Test-Path "publish") { Remove-Item "publish" -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path "Aplicativo.exe") { Remove-Item "Aplicativo.exe" -Force -ErrorAction SilentlyContinue }
    if (Test-Path "RotinaAddonAnydesk.exe") { Remove-Item "RotinaAddonAnydesk.exe" -Force -ErrorAction SilentlyContinue }
    if (Test-Path "aplicativo-Setup.exe") { Remove-Item "aplicativo-Setup.exe" -Force -ErrorAction SilentlyContinue }
    if (Test-Path "RotinaAddonAnydesk-Setup.exe") { Remove-Item "RotinaAddonAnydesk-Setup.exe" -Force -ErrorAction SilentlyContinue }
    Write-BuildLog "Diretórios de build limpos com sucesso."

    # Step 2: Process icon.png to .ico format for .exe and Inno Setup
    Write-BuildLog "Etapa 2/12: A processar o ícone icon.png para .exe e instalador..."
    if (Test-Path "icon.png") {
        Convert-PngToIco -pngPath "icon.png" -icoPath "app.ico" | Out-Null
        Convert-PngToIco -pngPath "icon.png" -icoPath "icon.ico" | Out-Null
        Convert-PngToIco -pngPath "icon.png" -icoPath "src/AnyDeskMonitor.Agent/app.ico" | Out-Null
        Write-BuildLog "Ícone 'icon.png' convertido e aplicado com sucesso em app.ico, icon.ico e src/AnyDeskMonitor.Agent/app.ico."
    } else {
        Write-BuildLog "AVISO: 'icon.png' não encontrado no diretório raiz."
    }

    # Step 3: Restore dependencies
    Write-BuildLog "Etapa 3/12: A restaurar dependências NuGet..."
    $restoreOut = dotnet restore AnyDeskMonitor.sln 2>&1
    Write-BuildLog ($restoreOut -join "`n")
    if ($LASTEXITCODE -ne 0) { throw "Falha na restauração de dependências NuGet." }

    # Step 4: Validate environment
    Write-BuildLog "Etapa 4/12: A validar ambiente de desenvolvimento..."
    $dotnetVer = dotnet --version
    Write-BuildLog "SDK .NET detetado: $dotnetVer"

    $isccPath = $null
    $possibleIsccPaths = @(
        "ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $possibleIsccPaths) {
        $cmd = Get-Command $path -ErrorAction SilentlyContinue
        if ($cmd) { $isccPath = $cmd.Path; break }
        if (Test-Path $path) { $isccPath = $path; break }
    }
    if ($isccPath) {
        Write-BuildLog "Inno Setup Compiler detetado em: $isccPath"
    } else {
        Write-BuildLog "AVISO: Inno Setup Compiler (ISCC.exe) não foi encontrado no PATH."
    }

    # Step 5: Build solution
    Write-BuildLog "Etapa 5/12: A compilar a Solution (Release)..."
    $buildOut = dotnet build AnyDeskMonitor.sln -c Release --no-restore 2>&1
    Write-BuildLog ($buildOut -join "`n")
    if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar a Solution." }

    # Step 6: Execute unit tests
    Write-BuildLog "Etapa 6/12: A executar testes unitários (xUnit)..."
    $testOut = dotnet test tests/AnyDeskMonitor.Tests/AnyDeskMonitor.Tests.csproj -c Release --no-build 2>&1
    Write-BuildLog ($testOut -join "`n")
    if ($LASTEXITCODE -ne 0) { throw "Falha na execução dos testes unitários." }

    # Step 7: Publish API
    Write-BuildLog "Etapa 7/12: A publicar ASP.NET Core Web API..."
    $pubApiOut = dotnet publish src/AnyDeskMonitor.Api/AnyDeskMonitor.Api.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish/Api 2>&1
    Write-BuildLog ($pubApiOut -join "`n")

    # Step 8: Publish Web
    Write-BuildLog "Etapa 8/12: A publicar Dashboard Blazor Web..."
    $pubWebOut = dotnet publish src/AnyDeskMonitor.Web/AnyDeskMonitor.Web.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish/Web 2>&1
    Write-BuildLog ($pubWebOut -join "`n")

    # Step 9: Publish Agent Portable EXE with embedded icon
    Write-BuildLog "Etapa 9/12: A publicar Agente Windows (RotinaAddonAnydesk Portable Single File)..."
    $pubAgentOut = dotnet publish src/AnyDeskMonitor.Agent/AnyDeskMonitor.Agent.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish/Agent 2>&1
    Write-BuildLog ($pubAgentOut -join "`n")
    if ($LASTEXITCODE -ne 0) { throw "Falha na publicação do agente." }

    # Step 10: Generate Portable EXE artifacts
    Write-BuildLog "Etapa 10/12: A gerar ficheiros executáveis portáteis..."
    $agentExe = Join-Path "publish/Agent" "RotinaAddonAnydesk.exe"
    if (-not (Test-Path $agentExe)) {
        $agentExe = Join-Path "publish/Agent" "AnyDeskMonitor.Agent.exe"
    }
    if (Test-Path $agentExe) {
        Copy-Item $agentExe -Destination "RotinaAddonAnydesk.exe" -Force
        Copy-Item $agentExe -Destination "Aplicativo.exe" -Force
        Copy-Item $agentExe -Destination "publish/Aplicativo.exe" -Force
        if (-not (Test-Path "publish/Agent/RotinaAddonAnydesk.exe")) {
            Copy-Item $agentExe -Destination "publish/Agent/RotinaAddonAnydesk.exe" -Force
        }
        Write-BuildLog "Executáveis 'RotinaAddonAnydesk.exe' e 'Aplicativo.exe' gerados com sucesso com o ícone de icon.png."
    } else {
        throw "Executável do agente não foi encontrado em publish/Agent."
    }

    # Step 11: Run Inno Setup Compiler with icon.png / icon.ico
    Write-BuildLog "Etapa 11/12: A compilar instalador Inno Setup utilizando o ícone..."
    if ($isccPath) {
        New-Item -ItemType Directory -Path "publish/Installer" -Force | Out-Null
        $isccOut = & $isccPath "setup.iss" 2>&1
        Write-BuildLog ($isccOut -join "`n")
        if ($LASTEXITCODE -eq 0) {
            $setupExe = Join-Path "publish/Installer" "RotinaAddonAnydesk-Setup.exe"
            if (Test-Path $setupExe) {
                Copy-Item $setupExe -Destination "aplicativo-Setup.exe" -Force
                Copy-Item $setupExe -Destination "RotinaAddonAnydesk-Setup.exe" -Force
                Write-BuildLog "Instaladores 'RotinaAddonAnydesk-Setup.exe' e 'aplicativo-Setup.exe' criados com sucesso com o ícone."
            }
        } else {
            Write-BuildLog "AVISO: Erro durante a compilação do Inno Setup."
        }
    } else {
        Write-BuildLog "AVISO: Inno Setup não instalado. Ignorando compilação do instalador .exe."
    }

    # Step 12: Validate files & logging
    Write-BuildLog "Etapa 12/12: A validar ficheiros gerados..."
    $filesToValidate = @(
        "RotinaAddonAnydesk.exe",
        "Aplicativo.exe",
        "publish/Agent/RotinaAddonAnydesk.exe",
        "publish/Installer/RotinaAddonAnydesk-Setup.exe",
        "RotinaAddonAnydesk-Setup.exe",
        "aplicativo-Setup.exe"
    )
    foreach ($f in $filesToValidate) {
        if (Test-Path $f) {
            $size = (Get-Item $f).Length
            Write-BuildLog "Ficheiro validado: $f ($size bytes)"
        } else {
            Write-BuildLog "AVISO: Ficheiro não encontrado: $f"
        }
    }

    # Final Summary Report
    $duration = (Get-Date) - $startTime
    Write-BuildLog "========================================================="
    Write-BuildLog "  BUILD CONCLUÍDO COM SUCESSO"
    Write-BuildLog "  Tempo Total de Execução: $($duration.TotalSeconds.ToString('F2'))s"
    Write-BuildLog "========================================================="

} catch {
    $err = $_.Exception.Message
    Write-BuildLog "---------------------------------------------------------"
    Write-BuildLog "  ERRO NO BUILD: $err"
    Write-BuildLog "  BUILD FAILED"
    Write-BuildLog "---------------------------------------------------------"
    exit 1
}
