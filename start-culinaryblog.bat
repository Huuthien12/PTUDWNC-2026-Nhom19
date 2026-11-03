@echo off
setlocal
title CulinaryBlog - Quick Start

cd /d "%~dp0"

echo ==========================================
echo   CULINARY BLOG - QUICK START
echo ==========================================
echo.

where docker >nul 2>&1 || (
  echo [ERROR] Docker CLI not found.
  echo Please install/start Docker Desktop once, then run this file again.
  pause
  exit /b 1
)

where dotnet >nul 2>&1 || (
  echo [ERROR] .NET SDK not found.
  pause
  exit /b 1
)

where npm >nul 2>&1 || (
  echo [ERROR] Node.js/npm not found.
  pause
  exit /b 1
)

echo [1/4] Starting Docker Desktop if needed...
docker info >nul 2>&1
if errorlevel 1 (
  if exist "%ProgramFiles%\Docker\Docker\Docker Desktop.exe" (
    start "" "%ProgramFiles%\Docker\Docker\Docker Desktop.exe"
    echo Waiting for Docker Engine...
    :waitDocker
    timeout /t 3 /nobreak >nul
    docker info >nul 2>&1
    if errorlevel 1 goto waitDocker
  ) else (
    echo [ERROR] Docker Engine is not running and Docker Desktop was not found.
    pause
    exit /b 1
  )
)

echo [2/4] Starting PostgreSQL, Redis, MinIO and Mailhog...
docker compose up -d
if errorlevel 1 (
  echo [ERROR] docker compose up failed.
  pause
  exit /b 1
)

echo [3/4] Starting backend...
start "CulinaryBlog Backend" cmd /k "cd /d ""%~dp0"" && dotnet run --project backend\CulinaryBlog.API"

echo [4/4] Starting frontend...
if not exist "%~dp0frontend\node_modules" (
  echo Installing frontend dependencies for the first run...
  call npm --prefix "%~dp0frontend" install
  if errorlevel 1 (
    echo [ERROR] npm install failed.
    pause
    exit /b 1
  )
)
start "CulinaryBlog Frontend" cmd /k "cd /d ""%~dp0frontend"" && npm run dev"

echo.
echo ==========================================
echo Project startup commands have been launched.
echo Backend : http://localhost:5062
echo Frontend: http://localhost:3000
echo ==========================================
echo.
echo Waiting a few seconds, then opening the website...
timeout /t 7 /nobreak >nul
start "" "http://localhost:3000"

exit /b 0
