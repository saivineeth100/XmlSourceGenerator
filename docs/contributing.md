# Contributing

Thank you for considering contributing to XmlSourceGenerator!

## Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- IDE: Visual Studio 2022, VS Code, or Rider

### Clone and Build

```bash
git clone https://github.com/saivineeth/XmlSourceGenerator.git
cd XmlSourceGenerator
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Project Structure

```
XmlSourceGenerator/
├── src/
│   ├── XmlSourceGenerator.Abstractions/   # Runtime library
│   └── XmlSourceGenerator/                # Source generator
├── tests/
│   └── XmlSourceGenerator.Tests/          # Integration tests
└── docs/                                  # Documentation
```

## Making Changes

### Code Style

- Follow existing code conventions
- Use C# latest language features where appropriate
- Add XML documentation comments for public APIs
- Keep methods focused and small

### Adding Features

1. **Plan:** Discuss major changes in an issue first
2. **Implement:** Write failing tests, then implement the feature
3. **Test:** Ensure all tests pass (54/55 target)
4. **Document:** Update relevant documentation in `docs/`

### Testing Guidelines

- Add tests for all new features
- Include both positive and negative test cases
- Test edge cases (nulls, empty collections, inheritance, etc.)
- Use descriptive test names: `TestFeature_Scenario_ExpectedResult`

**Test Categories:**
- `AttributesTests.cs` - Attribute functionality
- `IsolationTests.cs` - Individual features in isolation
- `ComplexTests.cs` - Advanced scenarios (recursion, nesting)
- `OverrideTests.cs` - Property overriding/hiding
- `XmlSerializerPerspectiveTests.cs` - XmlSerializer compatibility

## Pull Request Process

1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/my-feature`
3. **Commit** with clear messages: `git commit -m "Add XmlInclude attribute support"`
4. **Push** to your fork: `git push origin feature/my-feature`
5. **Open** a Pull Request with:
   - Clear description of changes
   - Link to related issue (if applicable)
   - Test results showing all tests pass

### PR Checklist

- [ ] Code follows existing style
- [ ] Tests added/updated and passing
- [ ] Documentation updated
- [ ] No breaking changes (or clearly documented)
- [ ] Commit messages are clear

## Debugging Source Generators

### View Generated Code

**Visual Studio:**
- Solution Explorer → Dependencies → Analyzers → XmlSourceGenerator
- Expand to see generated `.g.cs` files

**Command Line:**
```bash
dotnet build -p:EmitCompilerGeneratedFiles=true
# Generated files in: obj/Debug/net8.0/generated/
```

### Debug the Generator

1. Add `Debugger.Launch()` in `XmlSourceGenerator.cs`:
   ```csharp
   public void Execute(GeneratorExecutionContext context)
   {
       #if DEBUG
       if (!Debugger.IsAttached) Debugger.Launch();
       #endif
       // ... rest of code
   }
   ```

2. Build the test project
3. A debugger prompt will appear
4. Attach Visual Studio and debug

## Release Process

Releases are automated via GitHub Actions:

1. Update version in `Directory.Build.props` (if needed - MinVer handles it)
2. Create and push a tag: `git tag v1.0.0 && git push --tags`
3. GitHub Actions will:
   - Build and test
   - Create NuGet package
   - Publish to NuGet.org
   - Create GitHub release

## Code of Conduct

- Be respectful and inclusive
- Provide constructive feedback
- Focus on the issue, not the person
- Help others learn and grow

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Questions?

- Open an issue for bugs or feature requests
- Start a discussion for questions or ideas
- Tag maintainers for urgent issues

Thank you for making XmlSourceGenerator better! 🎉
