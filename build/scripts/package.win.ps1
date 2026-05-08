Remove-Item -Path build\GetHub\*.pdb -Force
Compress-Archive -Path build\GetHub -DestinationPath "build\gethub_${env:VERSION}.${env:RUNTIME}.zip" -Force