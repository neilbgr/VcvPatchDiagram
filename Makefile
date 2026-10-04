CLI     := src/VcvPatchDiagram.Cli
WEB     := src/VcvPatchDiagram.Web
CONFIG  := Release
PLUGINS ?= ../Cardinal/plugins

.PHONY: build test format catalog web publish-linux publish-win publish-web publish-all clean

build:
	dotnet build

test:
	dotnet test

format:
	dotnet format

# Regenerates catalog/ports.json from the Cardinal plugin sources (rebuild afterwards: it is embedded in Core).
catalog:
	dotnet run --project $(CLI) -- catalog build --src $(PLUGINS) --out catalog/ports.json --report catalog/scan-report.txt
	dotnet build

# Local dev server for the Blazor app: http://localhost:5016 (add ?sample=AmbientJam to load the sample).
web:
	dotnet run --project $(WEB) --launch-profile http

publish-linux:
	dotnet publish $(CLI) -c $(CONFIG) -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64

publish-win:
	dotnet publish $(CLI) -c $(CONFIG) -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win-x64

# Static site in publish/web/wwwroot: can be served by any static file server.
publish-web:
	dotnet publish $(WEB) -c $(CONFIG) -o publish/web

publish-all: publish-linux publish-win publish-web

clean:
	rm -rf publish out
	dotnet clean
