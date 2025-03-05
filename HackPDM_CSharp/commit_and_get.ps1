param (
    [string]$projectDir,
    [string]$publishVersion,
    [string]$revision
)

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

    
    
