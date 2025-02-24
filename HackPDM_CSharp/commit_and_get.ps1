param (
    [string]$projectDir,
    [string]$config
)

if ($config -eq "GitRelease")
{
    echo "--GitRelease--"
    # Change to the project directory
    cd $projectDir
    cd ..

    # Stage all changes
    git add .

    # Commit the changes
    git commit -m "Automated commit from build"
    git push -f

    # Get the current commit hash and commit date
    $commitHash = git log -1 --format="%H"
    $commitDate = git log -1 --format="%cd" --date=iso

    # go back into project folder
    cd $projectDir

    # Write the commit hash to a file
    # $commitHash | Out-File -FilePath

    $infoContent = @"
using System.Reflection;

[assembly: AssemblyInformationalVersion("Commit: $commitHash, Date: $commitDate")]
"@
    $infoFilePath = Join-Path -Path $projectDir -ChildPath "GitInfo.cs"
    Set-Content -Path $infoFilePath -Value $infoContent
}