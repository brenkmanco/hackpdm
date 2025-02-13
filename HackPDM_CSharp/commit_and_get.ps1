# Change to the project directory
cd $env:ProjectDir
cd ..
$actualProjectDirectory = (Get-Location).Path

# Stage all changes
git add .

# Commit the changes
git commit -m "Automated commit from build"

# Get the current commit hash and commit date
$commitHash = git log -1 --format="%H"
$commitDate = git log -1 --format="%cd" --date=iso

# Write the commit hash to a file
# $commitHash | Out-File -FilePath

$infoContent = @" 
[assembly: System.Reflection.AssemblyMetadata("CommitHash", "$commitHash")]
[assembly: System.Reflection.AssemblyMetadata("CommitDate", "$commitDate")]
"@
$infoFilePath = Join-Path -Path $actualProjectDirectory -ChildPath "GitInfo.cs"
Set-Content -Path $infoFilePath -Value $infoContent