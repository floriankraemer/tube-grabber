# Yt-dlp-ui — Makefile (same targets as make.ps1)
#
# All targets execute inside the dev container (bind-mounts this repo at /src).
# Escape hatch: IN_CONTAINER=1 make <target>

SLN := YtDlpUi.slnx
UI := src/YtDlpUi.UI/YtDlpUi.UI.csproj
COV_DIR := $(CURDIR)/artifacts/coverage
UI_APP_NAME := YtDlpUi

_DOTNET_RID := $(shell dotnet --info 2>/dev/null | sed -n 's/^[[:space:]]*RID:[[:space:]]*//p' | head -1)
PUBLISH_RID = $(if $(strip $(RID)),$(strip $(RID)),$(if $(strip $(_DOTNET_RID)),$(_DOTNET_RID),linux-x64))

CONTAINER_RUNNER ?= docker compose

ifeq ($(strip $(IN_CONTAINER)),1)
  _NATIVE := 1
else ifneq ($(wildcard /.dockerenv),)
  _NATIVE := 1
else
  _NATIVE :=
endif

_CONTAINER_RUN = $(CONTAINER_RUNNER) run --rm dev make $@ RID="$(RID)" IN_CONTAINER=1

.DEFAULT_GOAL := help
.PHONY: help restore build-debug build build-release build-ui test test-debug test-release clean ci publish-ui publish-ui-win coverage mutation-test

publish-ui-win: RID=win-x64

help:
	@echo "Yt-dlp-ui — Makefile"
	@echo "  make build-release     compile Release DLLs (not a standalone app)"
	@echo "  make publish-ui        self-contained app -> artifacts/publish-ui-<RID>/YtDlpUi"
	@echo "  make publish-ui-win    same, RID=win-x64"
	@echo "  make ci | test | coverage"

ifeq ($(_NATIVE),1)

restore:
	dotnet restore $(SLN)

build-debug:
	dotnet build $(SLN) -c Debug

build: build-debug

build-release:
	dotnet build $(SLN) -c Release

build-ui:
	dotnet build $(UI) -c Release

test:
	dotnet test $(SLN) -c Release --verbosity normal

test-debug:
	dotnet test $(SLN) -c Debug --verbosity normal

test-release:
	dotnet test $(SLN) -c Release --verbosity normal

clean:
	@find . -type d -name bin -prune -exec rm -rf {} +
	@find . -type d -name obj -prune -exec rm -rf {} +

ci:
	dotnet restore $(SLN)
	dotnet build $(SLN) -c Release --no-restore
	dotnet test $(SLN) -c Release --verbosity normal --no-build

publish-ui:
	@echo "Publishing UI for RID: $(PUBLISH_RID)"
	dotnet publish $(UI) -c Release -r $(PUBLISH_RID) --self-contained true -o artifacts/publish-ui-$(PUBLISH_RID) \
		/p:PublishSingleFile=true \
		/p:EnableCompressionInSingleFile=true \
		/p:IncludeNativeLibrariesForSelfExtract=true
	@ui_ext=""; case "$(PUBLISH_RID)" in win*) ui_ext=".exe";; esac; \
	output_dir="artifacts/publish-ui-$(PUBLISH_RID)"; \
	source_path="$$output_dir/YtDlpUi.UI$$ui_ext"; \
	target_path="$$output_dir/$(UI_APP_NAME)$$ui_ext"; \
	if [ -f "$$source_path" ] && [ "$$source_path" != "$$target_path" ]; then mv "$$source_path" "$$target_path"; fi

publish-ui-win: publish-ui

coverage:
	mkdir -p $(COV_DIR)
	dotnet tool restore
	dotnet test $(SLN) -c Release --verbosity minimal \
		/p:CollectCoverage=true \
		/p:CoverletOutputFormat=cobertura
	test -f $(COV_DIR)/YtDlpUi.Core.Tests.cobertura.xml
	test -f $(COV_DIR)/YtDlpUi.UI.Tests.cobertura.xml
	rm -rf $(COV_DIR)/html
	dotnet tool run reportgenerator -- \
		-reports:artifacts/coverage/*.cobertura.xml \
		-targetdir:artifacts/coverage/html \
		-reporttypes:Html

mutation-test:
	dotnet tool restore
	cd src/YtDlpUi.Core && (command -v dotnet-stryker >/dev/null 2>&1 && dotnet-stryker || dotnet tool run dotnet-stryker)

else

restore build-debug build build-release build-ui test test-debug test-release clean ci publish-ui publish-ui-win coverage mutation-test:
	$(_CONTAINER_RUN)

endif
