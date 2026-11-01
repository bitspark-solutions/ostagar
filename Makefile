# New-tree Makefile. The legacy one lives at legacy/Makefile and is frozen.
# Every target that reports success must be backed by verify-all.sh, not by optimism.

.DEFAULT_GOAL := help
SHELL := /bin/bash

# --env-file is required: compose resolves .env relative to the compose file's own directory
# (infra/), not the repository root, so without it ${POSTGRES_PASSWORD} never interpolates.
COMPOSE_DEV := docker compose --env-file .env -f infra/compose.dev.yml
DOTNET := dotnet

.PHONY: help verify verify-quick build restore test up down logs dev-reset migrate db-shell

help:
	@echo "Garments ERP — new tree"
	@echo ""
	@echo "  make verify        the gate: build + laws + tests + frontend + empty-db migrations"
	@echo "  make verify-quick  same, skipping container-backed stages"
	@echo "  make build         build the solution (warnings are errors)"
	@echo "  make test          run all test projects"
	@echo "  make up            build and start the whole stack: postgres, redis, migrate, api (:41983), web (:42107)"
	@echo "  make down          stop the stack"
	@echo "  make logs          follow stack logs"
	@echo "  make dev-reset     destroy the dev database volume (asks first)"
	@echo "  make migrate       apply EF migrations through the one-shot migrate container"
	@echo "  make db-shell      psql into the dev database"

verify:
	@./verify-all.sh

verify-quick:
	@./verify-all.sh --quick

build:
	@$(DOTNET) build GarmentsERP.slnx -v q --nologo

restore:
	@$(DOTNET) restore GarmentsERP.slnx

test:
	@$(DOTNET) test GarmentsERP.slnx -v q --nologo

up:
	@$(COMPOSE_DEV) up -d --build

down:
	@$(COMPOSE_DEV) down

logs:
	@$(COMPOSE_DEV) logs -f

# Destructive and deliberately gated: a password that was once public means the old volume may
# hold data keyed to credentials we burned during the 2026-10-15 history scrub.
dev-reset:
	@echo "This deletes the dev Postgres volume. Type YES to confirm:"; \
	read answer; \
	[ "$$answer" = "YES" ] && $(COMPOSE_DEV) down -v && echo "volume removed" || echo "aborted"

# Migrations run through the CLI image, the same path the deploy takes — `dotnet ef` against the
# host would exercise a route the containers never use.
migrate:
	@$(COMPOSE_DEV) up -d postgres
	@$(COMPOSE_DEV) run --rm migrate db migrate

db-shell:
	@$(COMPOSE_DEV) exec postgres psql -U postgres -d garments
