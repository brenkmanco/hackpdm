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


    echo "DEBUG HASH BEFORE: $(git log -1 --format="%H")"
    echo "DEBUG DATE BEFORE: $(git log -1 --format="%cd" --date=iso)"

    # Commit the changes
    git commit -m "Automated commit from build"
    git push -f

    # Get the current commit hash and commit date
    $commitHash = git log -1 --format="%H"
    $commitDate = git log -1 --format="%cd" --date=iso

    echo "DEBUG HASH AFTER: $($commitHash)"
    echo "DEBUG DATE AFTER: $($commitDate)"

    # go back into project folder
    cd $projectDir

    # Write the commit hash to a file
    # $commitHash | Out-File -FilePath

    $infoContent = @"
using System.Reflection;

[assembly: AssemblyInformationalVersion("Commit: $commitHash, Date: $commitDate")]
"@
    $infoFilePath = Join-Path -Path $projectDir -ChildPath "GitInfo.cs"
    try
    {
        Set-Content -Path $infoFilePath -Value $infoContent -Force
    } 
    catch 
    {
        echo "Unable to write to file"
    }
    
}