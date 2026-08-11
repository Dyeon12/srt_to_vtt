@echo off
chcp 65001 > nul
set "BUILD_ENGINE=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%BUILD_ENGINE%" (
  echo .NET Framework 빌드 도구를 찾을 수 없습니다.
  pause
  exit /b 1
)
"%BUILD_ENGINE%" "KoreanSubtitleStudio.sln" /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
if errorlevel 1 (
  echo 빌드 실패
  pause
  exit /b 1
)
if not exist "배포" mkdir "배포"
copy /y "KoreanSubtitleStudio\bin\Release\SRTtoVTTConverter.exe" "배포\SRT-to-VTT-Converter-Windows-x64.exe" > nul
echo 빌드 완료: 배포\SRT-to-VTT-Converter-Windows-x64.exe
pause
