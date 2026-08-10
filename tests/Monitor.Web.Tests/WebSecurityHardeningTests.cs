using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Monitor.Web.Controllers;
using Monitor.Web.Models;
using Monitor.Web.Services;
using Xunit;

namespace Monitor.Web.Tests;

public sealed class WebSecurityHardeningTests
{
    [Fact]
    public async Task SecurityHeaders_AreCentralized_Strict_AndUsePerRequestNonce()
    {
        var first = new DefaultHttpContext();
        var second = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(first);
        await middleware.InvokeAsync(second);

        var firstCsp = first.Response.Headers.ContentSecurityPolicy.ToString();
        var firstNonce = SecurityHeadersMiddleware.GetNonce(first);
        var secondNonce = SecurityHeadersMiddleware.GetNonce(second);

        Assert.Equal("nosniff", first.Response.Headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", first.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("no-referrer", first.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("camera=(), microphone=(), geolocation=()", first.Response.Headers["Permissions-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", firstCsp, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", firstCsp, StringComparison.Ordinal);
        Assert.Contains("form-action 'self'", firstCsp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", firstCsp, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", firstCsp, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(firstNonce));
        Assert.Contains($"'nonce-{firstNonce}'", firstCsp, StringComparison.Ordinal);
        Assert.NotEqual(firstNonce, secondNonce);
    }

    [Fact]
    public void EveryMutatingControllerAction_RequiresAntiforgery()
    {
        var controllerTypes = typeof(AccountController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .ToArray();

        var unprotected = new List<string>();
        foreach (var controller in controllerTypes)
        {
            foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attributes = method.GetCustomAttributes(inherit: true).ToArray();
                var mutating = attributes.Any(attribute => attribute is HttpPostAttribute or HttpPutAttribute or HttpPatchAttribute or HttpDeleteAttribute);
                if (!mutating) continue;

                if (!attributes.Any(attribute => attribute is ValidateAntiForgeryTokenAttribute))
                    unprotected.Add($"{controller.Name}.{method.Name}");
            }
        }

        Assert.True(unprotected.Count == 0, $"Mutating actions missing antiforgery: {string.Join(", ", unprotected)}");
    }

    [Fact]
    public void SessionPolicy_EnforcesIdleAndAbsoluteLifetime()
    {
        var now = new DateTimeOffset(2026, 8, 11, 1, 0, 0, TimeSpan.Zero);
        var options = new WebSecurityOptions { SessionIdleMinutes = 30, SessionAbsoluteHours = 8, HstsDays = 365 };
        options.Validate();

        Assert.False(AbsoluteSessionCookieEvents.IsExpired(Principal(now.AddHours(-7)), now, options));
        Assert.True(AbsoluteSessionCookieEvents.IsExpired(Principal(now.AddHours(-8)), now, options));
        Assert.True(AbsoluteSessionCookieEvents.IsExpired(new ClaimsPrincipal(new ClaimsIdentity("cookie")), now, options));
        Assert.Throws<InvalidOperationException>(() => new WebSecurityOptions { SessionIdleMinutes = 60, SessionAbsoluteHours = 1 }.Validate());
    }

    [Fact]
    public void LoginLockout_IsBounded_UsesOpaqueKey_AndResetsAfterWindow()
    {
        var time = new MutableSecurityTimeProvider(new DateTimeOffset(2026, 8, 11, 1, 0, 0, TimeSpan.Zero));
        var limiter = new LoginAttemptLimiter(time);
        var key = LoginAttemptKey.Create(IPAddress.Parse("192.0.2.45"), "Admin.User");

        Assert.DoesNotContain("192.0.2.45", key, StringComparison.Ordinal);
        Assert.DoesNotContain("ADMIN.USER", key, StringComparison.OrdinalIgnoreCase);
        for (var index = 0; index < LoginAttemptLimiter.FailureLimit; index++)
            limiter.RecordFailure(key);

        Assert.False(limiter.IsAllowed(key));
        time.Advance(LoginAttemptLimiter.Window + TimeSpan.FromSeconds(1));
        Assert.True(limiter.IsAllowed(key));
    }

    [Fact]
    public void ForwardedHeaders_RequireExplicitTrustedForwarders()
    {
        var empty = new WebSecurityOptions();
        Assert.False(empty.HasTrustedForwarders);
        Assert.Throws<InvalidOperationException>(() => TrustedForwarderPolicy.Configure(new ForwardedHeadersOptions(), empty));

        var policy = new WebSecurityOptions
        {
            TrustedProxies = ["10.20.30.40"],
            TrustedNetworks = ["192.0.2.0/24"]
        };
        policy.Validate();
        var options = new ForwardedHeadersOptions();
        TrustedForwarderPolicy.Configure(options, policy);

        Assert.Equal(1, options.ForwardLimit);
        Assert.True(options.RequireHeaderSymmetry);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Contains(IPAddress.Parse("10.20.30.40"), options.KnownProxies);
        Assert.Single(options.KnownNetworks);
        Assert.DoesNotContain(IPAddress.Parse("10.20.30.41"), options.KnownProxies);
    }

    [Fact]
    public void HstsPolicy_IsExplicitAndRejectsWeakDurations()
    {
        var policy = new WebSecurityOptions();
        policy.Validate();

        Assert.True(policy.HstsIncludeSubDomains);
        Assert.InRange(policy.HstsDays, 180, 730);
        Assert.Throws<InvalidOperationException>(() => new WebSecurityOptions { HstsDays = 30 }.Validate());
    }

    [Fact]
    public void InputNormalization_FuzzRejectsUnsafeTokensAndRegistrationMetadata()
    {
        var random = new Random(20260811);
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._- ;=\r\n\t/\\";
        for (var sample = 0; sample < 250; sample++)
        {
            var length = random.Next(0, 120);
            var value = new string(Enumerable.Range(0, length).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            var normalized = SecurityInput.NormalizeOptionalToken(value, 80);
            if (normalized is null) continue;

            Assert.InRange(normalized.Length, 1, 80);
            Assert.All(normalized, character => Assert.True(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'));
        }

        Assert.Null(SecurityInput.NormalizeOptionalToken(new string('A', 81), 80));
        Assert.Null(SecurityInput.NormalizeOptionalToken("rule\r\nInjected", 80));
        Assert.Throws<ArgumentException>(() => new SqlServerEndpoint("sql01;Password=canary"));
        Assert.Throws<ArgumentException>(() => new SqlServerEndpoint("sql01.internal", instanceName: "INSTANCE;Data Source=evil"));
        Assert.Throws<ArgumentException>(() => new SqlServerEndpoint("sql01\n.internal"));
        Assert.Throws<ArgumentException>(() => new ServerRegistration(Guid.NewGuid(), new string('D', 121), new SqlServerEndpoint("sql01.internal"), SqlAuthenticationMode.IntegratedSecurity, null, true, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new ConnectionSecretReference("env:SAFE\nINJECT"));
    }

    [Fact]
    public void SqlConnectionMetadata_CannotInjectConnectionStringKeys()
    {
        var registration = new ServerRegistration(
            Guid.NewGuid(),
            "SQL Safe",
            new SqlServerEndpoint("sql01.internal", instanceName: "MSSQL01"),
            SqlAuthenticationMode.IntegratedSecurity,
            null,
            true,
            DateTimeOffset.UtcNow);

        var applicationName = "Monitor;Password=not-a-key;Data Source=not-a-host";
        var connectionString = SqlConnectionStringFactory.Create(registration, null, applicationName);
        var parsed = new SqlConnectionStringBuilder(connectionString);

        Assert.Equal("sql01.internal\\MSSQL01", parsed.DataSource);
        Assert.Equal("master", parsed.InitialCatalog);
        Assert.True(parsed.IntegratedSecurity);
        Assert.Equal(applicationName, parsed.ApplicationName);
        Assert.Equal(string.Empty, parsed.Password);
        Assert.Throws<ArgumentException>(() => new SqlServerEndpoint("sql01.internal;Initial Catalog=evil"));
    }

    [Fact]
    public void SqlCredentialValues_AreValuesNotConnectionStringSyntax()
    {
        var registration = new ServerRegistration(
            Guid.NewGuid(),
            "SQL Login Safe",
            new SqlServerEndpoint("sql02.internal", 1433),
            SqlAuthenticationMode.SqlLogin,
            new ConnectionSecretReference("env:SAFE"),
            true,
            DateTimeOffset.UtcNow);
        var secret = new SqlLoginSecret("reader;Data Source=evil", "p@ss;Initial Catalog=evil");

        var parsed = new SqlConnectionStringBuilder(SqlConnectionStringFactory.Create(registration, secret, "Monitor.Security.Tests"));

        Assert.Equal("sql02.internal,1433", parsed.DataSource);
        Assert.Equal("master", parsed.InitialCatalog);
        Assert.Equal(secret.Username, parsed.UserID);
        Assert.Equal(secret.Password, parsed.Password);
    }

    [Fact]
    public void SecretCanaries_DoNotEnterAuditTelemetryOrLoginAttemptKeys()
    {
        const string canary = "super-secret-canary-20260811";
        var time = new MutableSecurityTimeProvider(new DateTimeOffset(2026, 8, 11, 1, 0, 0, TimeSpan.Zero));
        var audit = new InMemoryAuditStore(time);
        audit.Append("Admin", "login", "development-admin", $"Password={canary}");
        var auditText = string.Join('|', audit.Read(0, 100).Select(item => item.ToString()));

        var telemetry = new MonitorTelemetry(time);
        telemetry.CollectorFailed($"Password={canary};Data Source=sql-secret");
        var telemetryText = telemetry.Snapshot().ToString();

        var loginKey = LoginAttemptKey.Create(IPAddress.Parse("198.51.100.9"), canary);

        Assert.DoesNotContain(canary, auditText, StringComparison.Ordinal);
        Assert.Contains("[redacted]", auditText, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, telemetryText, StringComparison.Ordinal);
        Assert.DoesNotContain(canary, loginKey, StringComparison.Ordinal);
        Assert.DoesNotContain("198.51.100.9", loginKey, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal Principal(DateTimeOffset startedAt)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "Admin"),
            new Claim(MonitorClaimTypes.SessionStartedUtc, startedAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))
        ], "cookie");
        return new ClaimsPrincipal(identity);
    }

    private sealed class MutableSecurityTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
