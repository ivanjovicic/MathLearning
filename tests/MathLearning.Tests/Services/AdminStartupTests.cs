using MathLearning.Application.Content;
using MathLearning.Admin.Data;
using MathLearning.Domain.Entities;
using MathLearning.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MathLearning.Tests.Services;

public sealed class AdminStartupTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminStartupTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AdminHost_RegistersMathContentSanitizer()
    {
        using var scope = _factory.Services.CreateScope();

        var sanitizer = scope.ServiceProvider.GetRequiredService<IMathContentSanitizer>();

        Assert.IsType<MathContentSanitizer>(sanitizer);
    }

    [Fact]
    public async Task Healthz_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void AdminDbContext_UsesSharedContentTableNames()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        Assert.Equal("Topics", db.Model.FindEntityType(typeof(Topic))?.GetTableName());
        Assert.Equal("Subtopics", db.Model.FindEntityType(typeof(Subtopic))?.GetTableName());
        Assert.Equal("QuestionTranslations", db.Model.FindEntityType(typeof(QuestionTranslation))?.GetTableName());
        Assert.Equal("OptionTranslations", db.Model.FindEntityType(typeof(OptionTranslation))?.GetTableName());
        Assert.Equal("QuestionSteps", db.Model.FindEntityType(typeof(QuestionStep))?.GetTableName());
        Assert.Equal("QuestionStepTranslations", db.Model.FindEntityType(typeof(QuestionStepTranslation))?.GetTableName());
    }
}
