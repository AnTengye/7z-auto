.PHONY: build publish publish-sc publish-fd clean

PROJECT_PATH = Auto7z.UI/Auto7z.UI.csproj
OUTPUT_DIR_SC = bin/publish/standalone
OUTPUT_DIR_FD = bin/publish/framework-dependent

build:
	dotnet build $(PROJECT_PATH)

# Default publish (Self-Contained)
publish: publish-sc

# Self-Contained (Standalone, includes Runtime, ~75MB)
publish-sc:
	dotnet publish $(PROJECT_PATH) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $(OUTPUT_DIR_SC)

# Framework-Dependent (Requires .NET 8 Runtime installed, <5MB)
publish-fd:
	dotnet publish $(PROJECT_PATH) -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $(OUTPUT_DIR_FD)

clean:
	dotnet clean $(PROJECT_PATH)
	rm -rf bin/publish
