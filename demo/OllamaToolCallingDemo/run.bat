@echo off
echo Building Ollama Tool Calling Demo...
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Running demo...
dotnet run --configuration Release
pause