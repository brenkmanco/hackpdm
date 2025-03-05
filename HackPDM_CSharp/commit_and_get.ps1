param (
    [string]$projectDir,
    [string]$publishVersion,
    [string]$revision,
    [string]$config
)

if ($config -eq "GitRelease")
{
    echo dir: $projectDir
    echo publish: $publishVersion
    echo reveision: $revision
    echo config: $config
    # Change to the project directory
    cd $projectDir
    cd ..

    echo $projectDir
    echo $publishVersion
    echo $revision

    # Stage all changes
    git add .

    # Commit the changes
    git commit -m "Automated commit from build"
    git tag -a $publishVersion -m "$publishVersion"
    git push -f
    git push origin $publishVersion
}    
    
