# GetStream .NET SDK

This is the official .NET SDK for GetStream's Feeds API.

For detailed setup instructions, see [Example](samples/ConsoleApp/Program.cs).

## Makefile Commands

The project includes a Makefile for common development tasks:

```bash
## Set env
export STREAM_API_KEY=your-api-key-here
export STREAM_API_SECRET=your-api-secret-here

# Run the main application
make run

# Run the sample application
make sample

# Additional commands available in Makefile
make test      # Run the test suite
make build     # Build the project
make clean     # Clean build artifacts
```

## Structure

The SDK is organized into several key components:

- `src/` - Core SDK implementation
  - `Client.cs` - Main HTTP client with authentication and request handling
  - `CommonClient.cs` - Shared client functionality and utilities
  - `CustomCode.cs` - Custom implementations and extensions
  - Several OpenAPI-generated files:
    - `feed.cs` - Generated FeedClient with all feeds API methods
    - `models.cs` - Generated response models
    - `requests.cs` - Generated request models
- `tests/` - Comprehensive test suite
  - `FeedClientTests.cs` - Unit tests for feed client
  - `FeedIntegrationTests.cs` - Integration tests
  - `FeedTests.cs` - General feed functionality tests
- `samples/` - Example applications and usage demos
  - `ConsoleApp/` - Console application demonstrating basic usage

## Development

The SDK development workflow:

1. **Core Components**
   - Manual implementation of core client functionality
   - Custom extensions and utilities for .NET-specific features
   - Comprehensive test coverage for all components
   - OpenAPI-generated code for complete API coverage

2. **Testing**
   - Unit tests for individual components
   - Integration tests for end-to-end functionality
   - Sample applications for usage demonstration

3. **Build and Run**
   - Use Makefile commands for common tasks
   - Regular testing and validation
   - Continuous integration checks

The SDK follows .NET best practices and conventions while providing a clean, maintainable codebase for GetStream's Feeds API integration.
