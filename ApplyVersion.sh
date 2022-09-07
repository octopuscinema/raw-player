#!/bin/sh

# Read version
versionStr=`cat Version`

# Update verison in macOS solution
sed -i "s/version =.*/version = $versionStr/g" ./Player.macOS.sln

# Update version in info.plist

bundleVersionKeyLine = $(awk '/CFBundleVersion/{ print NR; exit }' ./UI/macOS/Info.plist)

#bundleVersionKeyLine = "$(grep -n 'CFBundleVersion' ./UI/macOS/Info.plist | head -n 1 | cut -d: -f1)"

#bundleVersionKeyLine = "$(grep -n "<key>CFBundleVersion</key>" ./UI/macOS/Info.plist | head -n 1 | cut -d: -f1)"

#bundleVersionKeyLine = "$(awk '/line/{ print NR; exit }' ./UI/macOS/Info.plist)"

sed -i "${bundleVersionKeyLine}s/.*/	<string>$versionStr<\/string>/" ./UI/macOS/Info.plist