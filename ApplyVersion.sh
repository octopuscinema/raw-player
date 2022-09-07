#!/bin/sh

# Read version
versionStr=`cat Version`

# Update verison in macOS solution
sed -i '' -e "s/version =.*/version = $versionStr/g" ./Player.macOS.sln

# Update CFBundleVersion in Info.plist
bundleVersionKeyLine=$(awk '/<key>CFBundleVersion<\/key>/{ print NR; exit }' ./UI/macOS/Info.plist)
bundleVersionKeyLine=$((bundleVersionKeyLine+1))
sed -i '' -e "${bundleVersionKeyLine}s/.*/	<string>$versionStr<\/string>/" ./UI/macOS/Info.plist

# Update CFBundleShortVersionString in Info.plist
bundleShortVersionKeyLine=$(awk '/<key>CFBundleShortVersionString<\/key>/{ print NR; exit }' ./UI/macOS/Info.plist)
bundleShortVersionKeyLine=$((bundleShortVersionKeyLine+1))
sed -i '' -e "${bundleShortVersionKeyLine}s/.*/	<string>$versionStr<\/string>/" ./UI/macOS/Info.plist