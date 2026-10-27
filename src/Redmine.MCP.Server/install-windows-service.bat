@echo off
setlocal EnableExtensions

set "SERVICE_NAME=Redmine.MCP.Server"
set "DISPLAY_NAME=Redmine MCP Server"
set "EXE=%~dp0Redmine.MCP.Server.exe"
set "CONFIG=%~dp0config.yml"
set "LOG_DIR=%~dp0logs"

net session >nul 2>&1
if errorlevel 1 (
    echo Run this script as Administrator.
    exit /b 1
)

if not exist "%EXE%" (
    echo Executable not found: "%EXE%"
    echo Run this script from the publish/install folder.
    exit /b 1
)

if not exist "%CONFIG%" (
    echo Config file not found: "%CONFIG%"
    echo Place config.yml next to the executable before registering the service.
    exit /b 1
)

if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    if errorlevel 1 (
        echo Failed to create log directory: "%LOG_DIR%"
        exit /b 1
    )
)

sc create "%SERVICE_NAME%" binPath= "\"%EXE%\" --config \"%CONFIG%\" --log \"%LOG_DIR%\"" start= auto DisplayName= "%DISPLAY_NAME%"
if errorlevel 1 (
    echo Failed to create service "%SERVICE_NAME%".
    exit /b 1
)

sc description "%SERVICE_NAME%" "Redmine MCP HTTP server"
if errorlevel 1 (
    echo Service created, but failed to set description for "%SERVICE_NAME%".
    exit /b 1
)

echo Service "%SERVICE_NAME%" registered successfully.
exit /b 0
