# Change to the project directory
cd $env:ProjectDir
cd ..

# Stage all changes
git add .

# Commit the changes
git commit -m "Automated commit from build"

# Get the current commit hash and commit date
$commitHash = git log -1 --format="%H"
$commitDate = git log -1 --format="%cd" --date=iso

# go back into project folder
cd $env:ProjectDir

# Write the commit hash to a file
# $commitHash | Out-File -FilePath

$infoContent = @"
using System.Reflection;

[assembly: AssemblyInformationalVersion("Commit: $commitHash, Date: $commitDate")]
"@
$infoFilePath = Join-Path -Path $env:ProjectDir -ChildPath "GitInfo.cs"
Set-Content -Path $infoFilePath -Value $infoContent
echo $infoFilePath
echo $infoContent