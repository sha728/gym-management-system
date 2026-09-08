using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GymApi.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GymApi.Tests;

public class AuthorizationTests
{
    [Fact]
    public async Task DeleteSession_RequiresAdminRole()
    {
        using var factory = new GymApiFactory();
        using var memberClient = factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-Test-Role", "Member");

        var sessionId = Guid.NewGuid();
        var memberResponse = await memberClient.DeleteAsync($"/api/Sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var adminResponse = await adminClient.DeleteAsync($"/api/Sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.NotFound, adminResponse.StatusCode);
    }

    private sealed class GymApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Admin:Password"] = "test-password"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<GymDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<GymDbContext>>();
                services.AddDbContext<GymDbContext>(options =>
                    options.UseInMemoryDatabase("authorization-tests"));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.TestScheme,
                    _ => { });
            });
        }
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string TestScheme = "Test";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].FirstOrDefault() ?? "Member";
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, TestScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, TestScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}