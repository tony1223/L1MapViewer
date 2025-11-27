@echo off
setlocal enabledelayedexpansion

REM Build script for L1MapEditor and L1MonsterEditor
REM Usage: build.bat [map|monster|all]
REM Output: Single exe file packed into zip

set PROJECT_DIR=%~dp0
set PROJECT_FILE=%PROJECT_DIR%L1MapViewerCore.csproj
set PROGRAM_FILE=%PROJECT_DIR%Program.cs
set OUTPUT_DIR=%PROJECT_DIR%publish
set TEMP_DIR=%PROJECT_DIR%publish\temp

REM Generate version: 1.0.MMdd.HHmm (e.g., 1.0.1127.2248)
for /f "tokens=2 delims==" %%I in ('wmic os get localdatetime /value') do set datetime=%%I
set VERSION=1.0.%datetime:~4,4%.%datetime:~8,4%
echo Version: %VERSION%

REM Create output directory
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

REM Backup original Program.cs
copy "%PROGRAM_FILE%" "%PROGRAM_FILE%.bak" >nul

if "%1"=="" goto usage
if "%1"=="map" goto build_map
if "%1"=="monster" goto build_monster
if "%1"=="all" goto build_all
goto usage

:build_map
echo ========================================
echo Building L1MapEditor (MapForm)...
echo ========================================

REM Modify Program.cs to use MapForm
(
echo using L1FlyMapViewer;
echo using L1MapViewer;
echo using System.Text;
echo.
echo namespace L1MapViewerCore;
echo.
echo static class Program
echo {
echo     [STAThread]
echo     static void Main^(^)
echo     {
echo         Encoding.RegisterProvider^(CodePagesEncodingProvider.Instance^);
echo         ApplicationConfiguration.Initialize^(^);
echo         Application.Run^(new MapForm^(^)^);
echo     }
echo }
) > "%PROGRAM_FILE%"

REM Build single-file exe (requires .NET Runtime)
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false -o "%TEMP_DIR%\L1MapEditor" /p:Version=%VERSION% /p:AssemblyVersion=%VERSION% /p:FileVersion=%VERSION% /p:PublishSingleFile=true

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    goto restore
)

REM Rename executable
if exist "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.exe" (
    move /Y "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.exe" "%TEMP_DIR%\L1MapEditor\L1MapEditor.exe" >nul
)

REM Delete pdb file
if exist "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.pdb" del "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.pdb"
if exist "%TEMP_DIR%\L1MapEditor\L1MapEditor.pdb" del "%TEMP_DIR%\L1MapEditor\L1MapEditor.pdb"

REM Create zip using PowerShell
set ZIP_NAME=L1MapEditor_v%VERSION%.zip
echo Creating %ZIP_NAME%...
powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%TEMP_DIR%\L1MapEditor\L1MapEditor.exe' -DestinationPath '%OUTPUT_DIR%\%ZIP_NAME%' -Force"

REM Cleanup temp
rd /s /q "%TEMP_DIR%\L1MapEditor" 2>nul

echo.
echo L1MapEditor built successfully!
echo Output: %OUTPUT_DIR%\%ZIP_NAME%
goto restore

:build_monster
echo ========================================
echo Building L1MonsterEditor (Form1)...
echo ========================================

REM Modify Program.cs to use Form1
(
echo using L1FlyMapViewer;
echo using L1MapViewer;
echo using System.Text;
echo.
echo namespace L1MapViewerCore;
echo.
echo static class Program
echo {
echo     [STAThread]
echo     static void Main^(^)
echo     {
echo         Encoding.RegisterProvider^(CodePagesEncodingProvider.Instance^);
echo         ApplicationConfiguration.Initialize^(^);
echo         Application.Run^(new Form1^(^)^);
echo     }
echo }
) > "%PROGRAM_FILE%"

REM Build single-file exe (requires .NET Runtime)
dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false -o "%TEMP_DIR%\L1MonsterEditor" /p:Version=%VERSION% /p:AssemblyVersion=%VERSION% /p:FileVersion=%VERSION% /p:PublishSingleFile=true

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    goto restore
)

REM Rename executable
if exist "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.exe" (
    move /Y "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.exe" "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.exe" >nul
)

REM Delete pdb file
if exist "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.pdb" del "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.pdb"
if exist "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.pdb" del "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.pdb"

REM Create zip using PowerShell
set ZIP_NAME=L1MonsterEditor_v%VERSION%.zip
echo Creating %ZIP_NAME%...
powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.exe' -DestinationPath '%OUTPUT_DIR%\%ZIP_NAME%' -Force"

REM Cleanup temp
rd /s /q "%TEMP_DIR%\L1MonsterEditor" 2>nul

echo.
echo L1MonsterEditor built successfully!
echo Output: %OUTPUT_DIR%\%ZIP_NAME%
goto restore

:build_all
call :build_map_internal
call :build_monster_internal
goto restore

:build_map_internal
echo ========================================
echo Building L1MapEditor (MapForm)...
echo ========================================

(
echo using L1FlyMapViewer;
echo using L1MapViewer;
echo using System.Text;
echo.
echo namespace L1MapViewerCore;
echo.
echo static class Program
echo {
echo     [STAThread]
echo     static void Main^(^)
echo     {
echo         Encoding.RegisterProvider^(CodePagesEncodingProvider.Instance^);
echo         ApplicationConfiguration.Initialize^(^);
echo         Application.Run^(new MapForm^(^)^);
echo     }
echo }
) > "%PROGRAM_FILE%"

dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false -o "%TEMP_DIR%\L1MapEditor" /p:Version=%VERSION% /p:AssemblyVersion=%VERSION% /p:FileVersion=%VERSION% /p:PublishSingleFile=true

if exist "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.exe" (
    move /Y "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.exe" "%TEMP_DIR%\L1MapEditor\L1MapEditor.exe" >nul
)

if exist "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.pdb" del "%TEMP_DIR%\L1MapEditor\L1MapViewerCore.pdb"
if exist "%TEMP_DIR%\L1MapEditor\L1MapEditor.pdb" del "%TEMP_DIR%\L1MapEditor\L1MapEditor.pdb"

set ZIP_NAME=L1MapEditor_v%VERSION%.zip
echo Creating %ZIP_NAME%...
powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%TEMP_DIR%\L1MapEditor\L1MapEditor.exe' -DestinationPath '%OUTPUT_DIR%\%ZIP_NAME%' -Force"

rd /s /q "%TEMP_DIR%\L1MapEditor" 2>nul
echo L1MapEditor built! Version: %VERSION%
goto :eof

:build_monster_internal
echo ========================================
echo Building L1MonsterEditor (Form1)...
echo ========================================

(
echo using L1FlyMapViewer;
echo using L1MapViewer;
echo using System.Text;
echo.
echo namespace L1MapViewerCore;
echo.
echo static class Program
echo {
echo     [STAThread]
echo     static void Main^(^)
echo     {
echo         Encoding.RegisterProvider^(CodePagesEncodingProvider.Instance^);
echo         ApplicationConfiguration.Initialize^(^);
echo         Application.Run^(new Form1^(^)^);
echo     }
echo }
) > "%PROGRAM_FILE%"

dotnet publish "%PROJECT_FILE%" -c Release -r win-x64 --self-contained false -o "%TEMP_DIR%\L1MonsterEditor" /p:Version=%VERSION% /p:AssemblyVersion=%VERSION% /p:FileVersion=%VERSION% /p:PublishSingleFile=true

if exist "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.exe" (
    move /Y "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.exe" "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.exe" >nul
)

if exist "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.pdb" del "%TEMP_DIR%\L1MonsterEditor\L1MapViewerCore.pdb"
if exist "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.pdb" del "%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.pdb"

set ZIP_NAME=L1MonsterEditor_v%VERSION%.zip
echo Creating %ZIP_NAME%...
powershell -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%TEMP_DIR%\L1MonsterEditor\L1MonsterEditor.exe' -DestinationPath '%OUTPUT_DIR%\%ZIP_NAME%' -Force"

rd /s /q "%TEMP_DIR%\L1MonsterEditor" 2>nul
echo L1MonsterEditor built! Version: %VERSION%
goto :eof

:restore
REM Restore original Program.cs
copy "%PROGRAM_FILE%.bak" "%PROGRAM_FILE%" >nul
del "%PROGRAM_FILE%.bak" >nul

REM Cleanup temp directory
if exist "%TEMP_DIR%" rd /s /q "%TEMP_DIR%" 2>nul

echo.
echo ========================================
echo Build complete!
echo ========================================
goto end

:usage
echo.
echo Usage: build.bat [map^|monster^|all]
echo.
echo   map     - Build L1MapEditor (MapForm)
echo   monster - Build L1MonsterEditor (Form1)
echo   all     - Build both applications
echo.
echo Output: Single exe packed into zip
echo   - L1MapEditor_v%VERSION%.zip
echo   - L1MonsterEditor_v%VERSION%.zip
echo.
goto restore_if_exists

:restore_if_exists
if exist "%PROGRAM_FILE%.bak" (
    copy "%PROGRAM_FILE%.bak" "%PROGRAM_FILE%" >nul
    del "%PROGRAM_FILE%.bak" >nul
)

:end
endlocal
