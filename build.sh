#!/usr/bin/env bash

projectPath="."
outputDir="./publish"

rm -rf "$outputDir"

dotnet publish "$projectPath" -c Release -r linux-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$outputDir/linux" || exit 1

dotnet publish "$projectPath" -c Release -r win-x64 \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$outputDir/windows" || exit 1

echo "Publish complete!"
echo "Linux build: $outputDir/linux"
echo "Windows build: $outputDir/windows"