#!/bin/sh

tempPath='./temp'
mkdir -p $tempPath

version=`cat libjpeg-turbo-version`
libUrl="https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/${version}/libjpeg-turbo-${version}.dmg"

path="${tempPath}/libjpeg-turbo.dmg"
curl $libUrl -o $path -L

mountPoint='/Volumes/libjpeg-turbo'
hdiutil mount $path -mountpoint $mountPoint
sudo installer -pkg "${mountPoint}/libjpeg-turbo.pkg" -target /
hdiutil unmount $mountPoint

sudo mv /opt/libjpeg-turbo/lib/libjpeg.a /opt/libjpeg-turbo/lib/libjpeg-turbo.a