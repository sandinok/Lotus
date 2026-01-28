@echo off
echo Initializing Git...
git init
git config --local user.name "sandinook"
git config --local user.email "abelvassquez27@gmail.com"
git add .
git commit -m "Lotus WinUI Fixes"

echo Creating GitHub Repository (Public)...
echo You may be prompted to login...
gh repo create Lotus-WinUI --public --source=. --remote=origin --push

echo.
echo Process Complete. Your code should be on GitHub now.
pause
