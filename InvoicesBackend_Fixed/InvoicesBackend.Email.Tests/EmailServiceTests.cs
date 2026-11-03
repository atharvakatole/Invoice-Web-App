using System.Net;
using System.Text.Json;
using InvoicesBackend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InvoicesBackend.Email.Tests;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendOtpAsync_UsesBrevoApiWithConfiguredVerifiedSender()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = CreateService(handler);

        await service.SendOtpAsync("recipient@example.com", "123456", "registration");

        Assert.Equal("POST", handler.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Url);
        Assert.Equal("test-api-key", handler.ApiKey);
        using var json = JsonDocument.Parse(handler.Body!);
        Assert.Equal("atharvakatole1999@gmail.com", json.RootElement.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("recipient@example.com", json.RootElement.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("Your Invoicely verification code", json.RootElement.GetProperty("subject").GetString());
        Assert.Contains("123456", json.RootElement.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task SendAssistantInviteAsync_UsesConfiguredProductionLoginUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = CreateService(handler);

        await service.SendAssistantInviteAsync("assistant@example.com", "Sam", "Acme", "temporary-password");

        using var json = JsonDocument.Parse(handler.Body!);
        var html = json.RootElement.GetProperty("htmlContent").GetString();
        Assert.Contains("https://invoice-web-app-ebon.vercel.app/login", html);
        Assert.DoesNotContain("localhost", html);
    }

    [Fact]
    public async Task SendOtpAsync_ProviderFailureIsReportedAsDeliveryFailure()
    {
        var service = CreateService(new RecordingHandler(HttpStatusCode.Unauthorized));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendOtpAsync("recipient@example.com", "123456", "registration"));

        Assert.Contains("HTTP 401", error.Message);
    }

    [Fact]
    public async Task SendOtpAsync_BrevoRequiresAnApiKey()
    {
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = CreateService(handler, apiKey: "");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendOtpAsync("recipient@example.com", "123456", "registration"));

        Assert.Contains("Email:ApiKey", error.Message);
        Assert.Null(handler.Method);
    }

    [Theory]
    [InlineData("https://invoicely.example", "https://invoicely.example/login")]
    [InlineData("https://invoicely.example/", "https://invoicely.example/login")]
    [InlineData("https://invoicely.example/app", "https://invoicely.example/app/login")]
    public void BuildAssistantInviteLoginUrl_UsesConfiguredFrontendUrl(string baseUrl, string expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Email:FrontendBaseUrl"] = baseUrl })
            .Build();

        Assert.Equal(expected, EmailService.BuildAssistantInviteLoginUrl(config));
    }

    private static EmailService CreateService(RecordingHandler handler, string apiKey = "test-api-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Brevo",
                ["Email:ApiKey"] = apiKey,
                ["Email:FromAddress"] = "atharvakatole1999@gmail.com",
                ["Email:FromName"] = "Invoicely",
                ["Email:FrontendBaseUrl"] = "https://invoice-web-app-ebon.vercel.app"
            })
            .Build();
        var factory = new RecordingHttpClientFactory(handler);
        return new EmailService(config, NullLogger<EmailService>.Instance, new TestEnvironment(), factory);
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        public string? Method { get; private set; }
        public string? Url { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            Url = request.RequestUri?.ToString();
            ApiKey = request.Headers.GetValues("api-key").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class RecordingHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "InvoicesBackend.Email.Tests";
        public string EnvironmentName { get; set; } = Environments.Staging;
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
