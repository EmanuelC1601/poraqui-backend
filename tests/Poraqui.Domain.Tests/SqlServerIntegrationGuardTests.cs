namespace Poraqui.Domain.Tests;

public sealed class SqlServerIntegrationGuardTests
{
    [Fact]
    public void Missing_test_connection_skips_integration_test()
    {
        Assert.NotNull(SqlServerIntegrationFactAttribute.GetSkipReason(null, "Server=shared;Database=dev;"));
    }

    [Fact]
    public void Missing_shared_connection_skips_integration_test()
    {
        Assert.NotNull(SqlServerIntegrationFactAttribute.GetSkipReason("Server=sql;Database=test;", null));
    }

    [Fact]
    public void Shared_database_is_rejected_as_test_target()
    {
        var reason = SqlServerIntegrationFactAttribute.GetSkipReason(
            "Server=another-alias.example;Database=development;User Id=test;Password=not-used;",
            "Server=sql.example;Database=development;User Id=shared;Password=not-used;");

        Assert.Contains("shared development database", reason);
    }

    [Fact]
    public void Separate_database_on_same_server_is_allowed()
    {
        var reason = SqlServerIntegrationFactAttribute.GetSkipReason(
            "Server=sql.example;Database=isolated-tests;User Id=test;Password=not-used;",
            "Server=sql.example;Database=development;User Id=shared;Password=not-used;");

        Assert.Null(reason);
    }

    [Fact]
    public void Invalid_sql_connection_is_rejected()
    {
        var reason = SqlServerIntegrationFactAttribute.GetSkipReason(
            "this-is-not-a-connection-string", "Server=sql.example;Database=development;");

        Assert.Contains("not a valid SQL Server connection string", reason);
    }
}
