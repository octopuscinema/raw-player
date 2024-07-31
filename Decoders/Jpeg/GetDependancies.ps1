try {
	$version = [IO.File]::ReadAllText((Resolve-Path '.\libjpeg-turbo-version'))
	
	$targetPath = '.\libjpeg-turbo'
	New-Item -Force -Path $targetPath -ItemType Directory
	cd $targetPath
	
	$tempPath = '.\temp'
	New-Item -Force -Path $tempPath -ItemType Directory
	
	$libUrl = 'https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/' + $version + '/libjpeg-turbo-' + $version + '-vc64.exe'

	$path = $tempPath + '\libjpeg-turbo64.exe'
	Invoke-WebRequest $libUrl -OutFile $path

	$extractLibArgs = 'e ' + $path + ' -aoa lib\jpeg-static.lib'

	& 'C:\Program Files\7-Zip\7z.exe' $extractLibArgs.Split()
	
	$extractHeadersArgs = 'e ' + $path + ' -aoa include\*.h'
	& 'C:\Program Files\7-Zip\7z.exe' $extractHeadersArgs.Split()
	
	rm $tempPath -r -force
	cd ..
}
catch {
	exit 1
}