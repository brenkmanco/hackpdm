# Change to the project directory
cd $env:ProjectDir

# Stage all changes
git add .

# Commit the changes
git commit -m "Automated commit from build"

# Get the commit hash
$commitHash = git log -1 --format="%H | %cd" --date=iso

# Write the commit hash to a file
$commitHash | Out-File -FilePath