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

# Run unit tests: no credentials needed, anything in the Integration category is excluded
test: build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "TestCategory!=Integration" -- RunConfiguration.TreatNoTestsAsError=true

# Run specific test by name (usage: make test-one TEST_NAME=TestName)
test-one: build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "Name~$(TEST_NAME)"

# Run endpoint tests only
test-endpoints: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "FullyQualifiedName~FeedEndpointTests"

# Run the tests that talk to a live Stream app
test-integration: check-env build
	$(DOTNET) test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "TestCategory=Integration" -- RunConfiguration.TreatNoTestsAsError=true

# Run sample app
sample: check-env build
	$(DOTNET) run --project $(SAMPLE_PROJECT) --configuration $(CONFIGURATION)

# Watch tests (rerun on file changes)
watch-test:
	$(DOTNET) watch test $(TEST_PROJECT) --configuration $(CONFIGURATION) --filter "TestCategory!=Integration"

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
	@echo "  test           - Run unit tests (no credentials needed)"
	@echo "  test-one       - Run specific test (usage: make test-one TEST_NAME=TestName)"
	@echo "  test-endpoints - Run endpoint tests only"
	@echo "  test-integration - Run the tests that talk to a live Stream app"
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