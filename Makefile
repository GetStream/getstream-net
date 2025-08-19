# Variables
DOTNET = dotnet
SOLUTION = stream-feed-net.sln
TEST_PROJECT = tests/stream-feed-net-test.csproj
SAMPLE_PROJECT = samples/ConsoleApp/ConsoleApp.csproj
CONFIGURATION ?= Debug

# Setup development environment
setup-env: ## Copy .env.example to .env (you'll need to fill in your credentials)
	cp .env.example .env
	@echo "Don't forget to update .env with your actual GetStream credentials!"

# Required environment variables check
check-env:
	@if [ ! -f .env ] && [ -z "$(STREAM_API_KEY)" ]; then \
		echo "Error: Neither .env file nor STREAM_API_KEY environment variable is set"; \
		echo "Run 'make setup-env' to create .env file or set environment variables"; \
		exit 1; \
	fi

# Clean build artifacts
clean:
	$(DOTNET) clean
	rm -rf **/bin/ **/obj/

# Restore NuGet packages
restore:
	$(DOTNET) restore

# Build solution
build: restore
	$(DOTNET) build --configuration $(CONFIGURATION)

# Run all tests
test: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION)

# Run specific test by name (usage: make test-one TEST_NAME=TestName)
test-one: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "Name~$(TEST_NAME)"

# Run endpoint tests only
test-endpoints: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "FullyQualifiedName~FeedEndpointTests"

# Run integration tests only
test-integration: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "FullyQualifiedName~FeedIntegrationTests"

# Run sample app
sample: check-env build
	$(DOTNET) run --project $(SAMPLE_PROJECT) --configuration $(CONFIGURATION)

# Watch tests (rerun on file changes)
watch-test: check-env
	$(DOTNET) watch test $(TEST_PROJECT) --configuration $(CONFIGURATION)

# Watch sample app (rerun on file changes)
watch-sample: check-env
	$(DOTNET) watch run --project $(SAMPLE_PROJECT) --configuration $(CONFIGURATION)

# Default target
.DEFAULT_GOAL := build

# Help
help:
	@echo "Available targets:"
	@echo "  setup-env      - Copy .env.example to .env (you'll need to fill in your credentials)"
	@echo "  clean          - Clean build artifacts"
	@echo "  restore        - Restore NuGet packages"
	@echo "  build          - Build solution"
	@echo "  test           - Run all tests"
	@echo "  test-one       - Run specific test (usage: make test-one TEST_NAME=TestName)"
	@echo "  test-endpoints - Run endpoint tests only"
	@echo "  test-integration - Run integration tests only"
	@echo "  sample         - Run sample app"
	@echo "  watch-test     - Watch tests (rerun on file changes)"
	@echo "  watch-sample   - Watch sample app (rerun on file changes)"
	@echo ""
	@echo "Configuration options:"
	@echo "  1. Create .env file: make setup-env, then edit .env with your credentials"
	@echo "  2. Set environment variables: STREAM_API_KEY and STREAM_API_SECRET"
	@echo ""
	@echo "Other environment variables:"
	@echo "  CONFIGURATION     - Build configuration (default: Debug)"

.PHONY: setup-env check-env clean restore build test test-one test-endpoints test-integration sample watch-test watch-sample help 