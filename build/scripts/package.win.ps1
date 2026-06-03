Remove-Item -Path build\GetHub\*.pdb -Force

# Bundle "how to open" instructions (app is unsigned -> SmartScreen blocks it)
$howto = @"
GetHub for Windows
==================

If the app won't open ("Windows protected your PC", or nothing happens):
GetHub is not code-signed, so Windows SmartScreen blocks it. To run it:

OPTION 1 - Unblock the ZIP BEFORE extracting (recommended):
  1. Right-click the downloaded .zip -> Properties
  2. At the bottom, tick "Unblock" -> OK
  3. Now extract and run GetHub.exe

OPTION 2 - Allow it at the SmartScreen prompt:
  1. Double-click GetHub.exe
  2. On "Windows protected your PC", click "More info"
  3. Click "Run anyway"

Settings are stored in the "data" folder next to GetHub.exe (portable mode).
Requires Git for Windows: https://git-scm.com/download/win
"@
Set-Content -Path "build\GetHub\HOW-TO-OPEN.txt" -Value $howto -Encoding UTF8

Compress-Archive -Path build\GetHub -DestinationPath "build\gethub_${env:VERSION}.${env:RUNTIME}.zip" -Force
