set -e

# that sucks: we nees real workspaces here (please see TODO - there are many things to consider)

cd ./src/FsxWatcher
project="FsxWatcher.fsproj"

dotnet restore "$project"
dotnet build "$project" --configuration Release
dotnet pack "$project" --configuration Release --output ./.nupkg
dotnet nuget push ./.nupkg/*.nupkg -k "$NUGET_API_KEY" -s https://api.nuget.org/v3/index.json
