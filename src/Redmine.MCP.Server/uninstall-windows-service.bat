@echo off
setlocal EnableExtensions

set "SERVICE_NAME=Redmine.MCP.Server"

net session >nul 2>&1
if errorlevel 1 (
    echo Run this script as Administrator.
    exit /b 1
)

sc stop "%SERVICE_NAME%" >nul 2>&1
sc delete "%SERVICE_NAME%"
if errorlevel 1 (
    echo Failed to delete service "%SERVICE_NAME%".
    exit /b 1
)

echo Service "%SERVICE_NAME%" removed successfully.
exit /b 0
