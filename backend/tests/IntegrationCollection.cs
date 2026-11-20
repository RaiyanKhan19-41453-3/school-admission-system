using Xunit;

namespace Admissions.Tests;

/// <summary>Serialises tests that share a single PostgreSQL test database.</summary>
[CollectionDefinition("Integration", DisableParallelization = true)]
public class IntegrationCollection;
