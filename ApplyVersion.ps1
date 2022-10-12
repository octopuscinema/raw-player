try {
	$version = [IO.File]::ReadAllText((Resolve-Path ".\Version"))
	$doc = New-Object System.Xml.XmlDocument
	$doc.Load((Resolve-Path ".\UI\Windows\Player.UI.Windows.csproj"))
	$doc.Project.PropertyGroup.Version = $version
	$doc.Save((Resolve-Path ".\UI\Windows\Player.UI.Windows.csproj"))
	$installerVersionText = '"ProductVersion" = "8:' + $version + '"'
	(Get-Content ".\Installer\Installer.vdproj") -replace '"ProductVersion" = "8:.+"', "$installerVersionText" | Set-Content ".\Installer\Installer.vdproj"
	$installerFilename = '8:OCTOPUS-RAW-Player-' + $version + '-setup.msi'
	(Get-Content ".\Installer\Installer.vdproj") -replace '8:OCTOPUS-RAW-Player-setup.msi', "$installerFilename" | Set-Content ".\Installer\Installer.vdproj"
}
catch {
	exit 1
}