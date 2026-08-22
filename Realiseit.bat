@echo off

set VERSION=1.10
echo.
echo Creating release %VERSION%...
echo.

if exist ReleaseTemp rmdir /S /Q ReleaseTemp
mkdir ReleaseTemp

xcopy bin\Debug\* ReleaseTemp\ /E /I /Y

del ReleaseTemp\*.pdb /Q

if not exist Releases mkdir Releases

powershell Compress-Archive -Path ReleaseTemp\* -DestinationPath Releases\rrs2sql-%VERSION%.zip -Force

rmdir /S /Q ReleaseTemp

echo.
echo Release created:
echo Releases\rrs2sql-%VERSION%.zip
pause