try {
	$tempPath = '.\temp'
	New-Item -Force -Path $tempPath -ItemType Directory
	
	$version = [IO.File]::ReadAllText((Resolve-Path '.\Version'))
	$libUrl = 'https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/' + $version + '/libjpeg-turbo-' + $version + '-vc64.exe'

	$path = $tempPath + '\libjpeg-turbo64.exe'
	Invoke-WebRequest $libUrl -OutFile $path

	$extractArgs = 'e ' + $path + ' -aoa ' + ' bin\jpeg62.dll'
	& 'C:\Program Files\7-Zip\7z.exe' $extractArgs.Split()
	
	rm $tempPath -r -force
}
catch {
	exit 1
}