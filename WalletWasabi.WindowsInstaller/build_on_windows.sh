#!/bin/bash
# Bash for Windows rocks !
#
# This script works around the following error on Windows :

# $ dotnet run
# WalletWasabi.WindowsInstaller\WalletWasabi.WindowsInstaller.wixproj(78,5): error MSB4062: The "HeatDirectory" task could not be loaded from the assembly C:\wix311-binaries\WixTasks.dll. Could not load file or assembly 'Microsoft.Build.Utilities, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'. The system cannot find the file specified. Confirm that the <UsingTask> declaration is correct, that the assembly and all its dependencies are available, and that the task contains a public class that implements Microsoft.Build.Framework.ITask.
# 
# The build failed. Fix the build errors and run again.


# I spent a lot of time trying to get rid of that error but nothing helped ...
# So to build the msi on Windows , I run the commands ( heat.exe , candle.exe , light.exe ) manually now .
# I know it sucks but at least I'm able to create an MSI this way .

guidir="..\\WalletWasabi.Gui"
guibindistdir="$guidir\\bin\\dist"

# HEAT
# ####
# ComponentGroupName="PublishedComponents" becomes -cg PublishedComponents
# OutputFile="ComponentsGenerated.wxs"
# DirectoryRefId="INSTALLFOLDER" becomes -dr INSTALLFOLDER
# SuppressCom="true" becomes -scom
# Directory="C:\win7-x64" becomes dir ..\\WalletWasabi.Gui\\bin\\dist\\win7-x64
# SuppressFragments="true" becomes -sfrag
# SuppressRegistry="true" becomes -sreg
# SuppressRootDirectory="true" becomes -srd
# AutoGenerateGuids="false" becomes -ag
# GenerateGuidsNow="true" becomes -gg
# ToolPath="C:\wix311-binaries"
# PreprocessorVariable="var.BasePath" becomes -var var.BasePath

/c/wix311-binaries/heat.exe dir "$guibindistdir"\\win7-x64 -dr INSTALLFOLDER -cg PublishedComponents -gg -sfrag -scom -srd -sreg -var var.BasePath -out ComponentsGenerated.wxs

# Extract BuildVersion because candle.exe needs it ...
buildversionfile=../WalletWasabi/obj/Release/netcoreapp3.1/WalletWasabi.AssemblyInfo.cs
echo "Extracting BuildVersion from $buildversionfile"
BuildVersion=$(grep AssemblyVersion "$buildversionfile" | head -n 1 | cut -d '"' -f 2)
if [ -z "$BuildVersion" ]; then
	echo
	echo "ERROR: could not extract BuildVersion from $buildversionfile"
	echo
else
	echo "Extracted BuildVersion $BuildVersion from $buildversionfile"
	echo
	echo "CANDLE"
	echo "######"
	/c/wix311-binaries/candle.exe -dBuildVersion="$BuildVersion" -dWalletWasabi.Gui.ProjectDir="$guidir" -dBasePath="$guibindistdir"\\win7-x64 *.wxs

	echo
	echo "LIGHT"
	echo "#####"
	# grep -v "error LGHT0204 : ICE80: This 32BitComponent" fixes a warning regarding 32 bit into 64 bit Folders
	/c/wix311-binaries/light.exe -ext WixUIExtension *.wixobj -loc Common.wxl -spdb -o "$guibindistdir"\\MustardWalletLTC-"$BuildVersion".msi 2>&1 | grep -v "error LGHT0204 : ICE80: This 32BitComponent"
fi
echo "Build completed !"
explorer.exe "$guibindistdir"
