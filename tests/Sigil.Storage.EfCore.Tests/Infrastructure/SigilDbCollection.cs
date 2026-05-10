using Xunit;

namespace Sigil.Storage.EfCore.Tests.Infrastructure;

[CollectionDefinition("SigilDb")]
public sealed class SigilDbCollection : ICollectionFixture<PostgresFixture> { }
