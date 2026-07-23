.PHONY: help doctor restore build test scenario plugin-link import headless capture editor run

GODOT ?=
SCENARIO ?= bootstrap
CAPTURE ?= wall-cutaway

help:
	@pwsh -NoProfile -File scripts/dev.ps1 help

doctor:
	@pwsh -NoProfile -File scripts/dev.ps1 doctor -Godot "$(GODOT)"

restore:
	@pwsh -NoProfile -File scripts/dev.ps1 restore

build:
	@pwsh -NoProfile -File scripts/dev.ps1 build

test:
	@pwsh -NoProfile -File scripts/dev.ps1 test

scenario:
	@pwsh -NoProfile -File scripts/dev.ps1 scenario -Name "$(SCENARIO)"

plugin-link:
	@pwsh -NoProfile -File scripts/dev.ps1 plugin-link

import:
	@pwsh -NoProfile -File scripts/dev.ps1 import -Godot "$(GODOT)"

headless:
	@pwsh -NoProfile -File scripts/dev.ps1 headless -Name "$(SCENARIO)" -Godot "$(GODOT)"

capture:
	@pwsh -NoProfile -File scripts/dev.ps1 capture -Name "$(CAPTURE)" -Godot "$(GODOT)"

editor:
	@pwsh -NoProfile -File scripts/dev.ps1 editor -Godot "$(GODOT)"

run:
	@pwsh -NoProfile -File scripts/dev.ps1 run -Godot "$(GODOT)"
