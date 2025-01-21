#!/usr/bin/env python3

import os
import sys
import platform
import argparse
import re
import shutil
import subprocess
from pathlib import Path


# To assign the correct platform moniker on the platform it's built on
def get_platform_and_architecture():
    os_platform = ""
    architecture = ""

    # Operating System
    if sys.platform == "win32":
        os_platform = "win"
    elif sys.platform == "darwin":
        os_platform = "osx"
    elif sys.platform == "linux":
        os_platform = sys.platform
    else:
        print("The OS type could not be determined.")
        exit()

    # CPU Architecture
    if (platform.machine() == 'x86_64') or (platform.machine() == 'amd64') or (platform.machine() == 'AMD64'):
        architecture = "x64"
    elif (platform.machine() == 'arm64') or (platform.machine() == 'ARM64') or (platform.machine() == 'aarch64') or (platform.machine() == 'Aarch64') or (platform.machine() == 'AARCH64'):
        architecture = "arm64"
    else:
        print("The CPU architecture type could not be determined.")
        exit()

    return os_platform + "-" + architecture


def create_csproj(mingw_folder, dotnet_rid, lib_paths):
    version = "1.6.3" # Version must be manually defined for now
    with open("org.adwaita.native." + dotnet_rid + ".csproj", "w") as f:
        csproj_strings = [
            "<Project Sdk='"'Microsoft.NET.Sdk'"'>", "\n",
            "  <PropertyGroup>", "\n",
            "    <PackageLicenseExpression>LGPL-2.1-or-later</PackageLicenseExpression>", "\n",
            "    <PackageReadmeFile>README.md</PackageReadmeFile>", "\n",
            "    <OutputType>Library</OutputType>", "\n",
            "    <TargetFrameworks>netstandard2.0;netcoreapp3.1;net6.0;net8.0</TargetFrameworks>", "\n",
            "    <NoWarn>NU5128</NoWarn>", "\n",
            "    <RuntimeIdentifier>" + dotnet_rid + "</RuntimeIdentifier>", "\n",
            "    <AssemblyName>Adwaita.Native</AssemblyName>", "\n",
            "    <Version>" + version + "</Version>", "\n",
            "\n",
            "    <PackageProjectUrl>https://github.com/PlatinumLucario/VGMusicStudio/tree/new-gui-experimental/CreateNugets</PackageProjectUrl>", "\n",
            "    <RepositoryUrl>https://gitlab.gnome.org/GNOME/libadwaita/</RepositoryUrl>", "\n",
            "    <PackageTags>adwaita libadwaita gtk glib gio native runtime</PackageTags>", "\n",
            "\n",
            "    <!-- Nuget Properties -->", "\n",
            "    <Description>", "\n",
            "    Building blocks for modern GNOME applications.", "\n",
            "    Source code repository: https://gitlab.gnome.org/GNOME/libadwaita/", "\n",
            "    </Description>", "\n",
            "    <IncludeBuildOutput>false</IncludeBuildOutput>", "\n",
            "\n",
            "    <!-- Pack Option -->", "\n",
            "    <Title>Adwaita " + dotnet_rid + " v" + version + "</Title>", "\n",
            "    <PackageId>org.adwaita.native." + dotnet_rid + "</PackageId>", "\n",
            "\n",
            "    <!-- Signing -->", "\n",
            "    <SignAssembly>false</SignAssembly>", "\n",
            "    <PublicSign>false</PublicSign>", "\n",
            "    <DelaySign>false</DelaySign>", "\n",
            "  </PropertyGroup>", "\n",
            "\n",
            "  <ItemGroup>", "\n",
            "    <None Include='"'./README.md'"' Pack='"'true'"' PackagePath='"'/'"'/>", "\n",
            "  </ItemGroup>", "\n",
            "\n",
            "  <ItemGroup>", "\n",
            "    <!-- Native libraries can only be in native directories -->", "\n"
            ]
        prev_path = "" # To prevent any duplicate paths, this string variable is made
        for path in lib_paths:
            if path == prev_path:
                continue # That way, if the path is identical to the previous, it'll continue to the next one
            # Append the path to the csproj strings
            csproj_strings.append(
                "    <NativeLibrary Include=" + '"' + "C:/msys64" + path + '"' + " " + "/>" + "\n"
            )
            prev_path = path
        csproj_strings.append(
            "    \n" +
            "     <!-- SVG pixbuf loader -->" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/libcharset-1.dll" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/librsvg-2-2.dll" + '"' + " " + "/>" + "\n"
            "     \n" +
            "     <!-- Executable binaries -->" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gdbus.exe" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gdk-pixbuf-query-loaders.exe" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gspawn-win64-helper.exe" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gspawn-win64-helper-console.exe" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gtk4-query-settings.exe" + '"' + " " + "/>" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/bin/gtk4-update-icon-cache.exe" + '"' + " " + "/>" + "\n"
            "     \n" +
            "     <!-- Shared asset folders -->" + "\n"
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/lib/gdk-pixbuff-2.0/**" + '"' + " " + "LinkBase=" + '"' + "../lib/gdk-pixbuff-2.0" + '"' + " " + "/>" + "\n" +
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/share/glib-2.0/schemas/**" + '"' + " " + "LinkBase=" + '"' + "../share/glib-2.0/schemas" + '"' + " " + "/>" + "\n" +
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/share/locale/**" + '"' + " " + "LinkBase=" + '"' + "../share/locale" + '"' + " " + "/>" + "\n" +
            "     <NativeLibrary Include=" + '"' + mingw_folder + "/share/icons/Adwaita/**" + '"' + " " + "LinkBase=" + '"' + "../share/icons/Adwaita" + '"' + " " + "/>" + "\n"
        )
        csproj_strings.append(
            " </ItemGroup>" + "\n" +
            "  \n" +
            "  <ItemGroup>" + "\n" +
            "    <Content Include='"'@(NativeLibrary)'"'>" + "\n" +
            "      <PackagePath>runtimes/" + dotnet_rid + "/native/%(Filename)%(Extension)</PackagePath>" + "\n" +
            "      <Pack>true</Pack>" + "\n" +
            "      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>" + "\n" +
            "    </Content>" + "\n"
            "  </ItemGroup>" + "\n" +
            "\n" +
            "</Project>" + "\n"
        )
        writable_strings = ' '.join(csproj_strings)
        f.write(writable_strings)


# The main function
def main():
    lib_paths = []
    dotnet_rid = ""
    mingw_folder = ""

    # Checks the Environment Variable
    if os.getenv("MinGWFolder") == None:
        mingw_folder = "C:/msys64/mingw64"
    else:
        mingw_folder = os.getenv("MinGWFolder")

    # Gets the output from the MSYS2 bash terminal
    bash_output = subprocess.run("C:/msys64/usr/bin/bash.exe -lc '"'ldd /mingw64/bin/libadwaita-1-0.dll | grep ''\\/mingw.*\\.dll'' -o'"'", stdout=subprocess.PIPE, text=True)

    # Splits the lines, appends them to a new list, then adds the main library to the end of the list
    if bash_output.returncode == 0:
        lib_paths = bash_output.stdout.splitlines()
        lib_paths.append("/mingw64/bin/libadwaita-1-0.dll")
    else:
        print("Error: MSYS2 or libadwaita cannot be found.\nPlease install MSYS2, then run 'pacman -S libadwaita' in MSYS2 to install libadwaita.\n")
        exit()

    # Appends platform moniker to variable
    dotnet_rid = get_platform_and_architecture()

    # Creates the csproj with the params
    create_csproj(mingw_folder, dotnet_rid, lib_paths)


if __name__ == "__main__":
    main()
