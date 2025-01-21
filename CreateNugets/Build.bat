::
:: Fetches the paths to the libraries and generates the csproj
::
:: C:/msys64/usr/bin/bash.exe -lc "ldd /mingw64/bin/libadwaita-1-0.dll | grep '\/mingw.*\.dll' -o" > output.txt
::
if exist VGMusicStudio.Dependencies.Adwaita.Native.win-x64.csproj (
    del VGMusicStudio.Dependencies.Adwaita.Native.win-x64.csproj
)
python generate-csproj.py
dotnet pack -c Release
