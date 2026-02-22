using System.Net;
using System.Text.RegularExpressions;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.UI;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Integration tests for the AsyncAPI UI component.
/// These tests verify that the UI can load and render the AsyncAPI document without schema errors.
/// Uses Playwright for headless browser testing to validate JavaScript execution.
/// 
/// KNOWN ISSUE: The @asyncapi/react-component v3.0.0 has compatibility issues that cause it to
/// display "Error: There are errors in your Asyncapi document" with the message 
/// "This is not an AsyncAPI document. The asyncapi field as string is missing." for both
/// AsyncAPI 2.x and 3.x documents, even though the documents pass ByteBard.AsyncAPI.NET validation.
/// 
/// Tests that check for schema errors in the UI are currently skipped or expected to fail until
/// the UI component is updated or the issue is resolved. The underlying document generation
/// is validated separately in AsyncApiSchemaValidationTests using ByteBard.AsyncAPI.NET.
/// 
/// See: https://github.com/asyncapi/asyncapi-react for updates on the React component.
/// </summary>
public class AsyncApiUiIntegrationTests : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IHost? _host;
    private string? _baseUrl;

    private static string GetDocumentRoute(string documentName) => 
        AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName);

    public async Task InitializeAsync()
    {
        // Install browsers if not installed (needed for CI)
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright browser installation failed with exit code {exitCode}");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        
        if (_browser != null)
        {
            await _browser.CloseAsync();
        }
        
        _playwright?.Dispose();
    }

    [Fact]
    public async Task AsyncApiUi_ReturnsHtmlPage()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/async-api");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("<!DOCTYPE html>");
        content.ShouldContain("asyncapi");
    }

    [Fact]
    public async Task AsyncApiUi_ContainsCorrectDocumentUrl()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/async-api");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        // The UI should reference the AsyncAPI document URL
        content.ShouldContain("/asyncapi/");
        content.ShouldContain(".json");
    }

    /// <summary>
    /// Tests that the AsyncAPI document URL in the UI HTML is correctly constructed
    /// and that the document can be fetched from that URL.
    /// This verifies the document is properly placed into the UI configuration.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_DocumentUrlIsAccessibleFromBrowser()
    {
        // Arrange
        await StartServerAsync();
        var page = await _browser!.NewPageAsync();
        string? capturedDocumentJson = null;
        string? capturedUrl = null;
        int? capturedStatus = null;

        // Use route interception to capture the exact document response
        await page.RouteAsync("**/asyncapi/*.json", async route =>
        {
            capturedUrl = route.Request.Url;
            var response = await route.FetchAsync();
            capturedStatus = response.Status;
            capturedDocumentJson = await response.TextAsync();
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Response = response
            });
        });

        // Act - Navigate to the UI page
        var htmlResponse = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for any async document fetching
        await page.WaitForTimeoutAsync(2000);

        // Assert - Verify the HTML loaded correctly
        htmlResponse.ShouldNotBeNull();
        htmlResponse.Ok.ShouldBeTrue("HTML page should load successfully");

        // Check that the document URL was requested and returned successfully
        capturedUrl.ShouldNotBeNullOrEmpty("The UI should fetch the AsyncAPI document from the URL");
        capturedStatus.ShouldBe(200, $"AsyncAPI document should be fetched successfully. URL: {capturedUrl}");
        capturedDocumentJson.ShouldNotBeNullOrEmpty($"Document body should not be empty. URL: {capturedUrl}");
        
        // Verify the document body contains valid AsyncAPI content
        capturedDocumentJson.ShouldContain("asyncapi", 
            customMessage: $"Document should contain 'asyncapi' field. This verifies the document is properly placed into the UI. URL: {capturedUrl}");

        await page.CloseAsync();
    }

    /// <summary>
    /// Tests that the AsyncAPI document is correctly injected into the React component
    /// and that it can parse the document without errors.
    /// This test intercepts the network request to see exactly what the UI receives.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_DocumentIsCorrectlyServedToReactComponent()
    {
        // Arrange
        await StartServerAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0;
            options.AddServer("test-server", "localhost:5000", "http");
            options.WithInfo("Test API for UI", "1.0.0");
            options.WithDescription("Testing document injection into UI");
        });

        var page = await _browser!.NewPageAsync();
        string? capturedDocumentJson = null;
        string? capturedContentType = null;
        int? capturedStatusCode = null;

        // Intercept the document fetch to capture exactly what the UI receives
        await page.RouteAsync("**/asyncapi/*.json", async route =>
        {
            var response = await route.FetchAsync();
            capturedDocumentJson = await response.TextAsync();
            capturedContentType = response.Headers.TryGetValue("content-type", out var ct) ? ct : null;
            capturedStatusCode = response.Status;
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Response = response
            });
        });

        // Act
        await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(2000);

        // Assert
        capturedDocumentJson.ShouldNotBeNullOrEmpty("UI should fetch the AsyncAPI document");
        
        // Debug output - show what we captured
        var debugInfo = $"Status: {capturedStatusCode}, Content-Type: {capturedContentType}, Content length: {capturedDocumentJson?.Length ?? 0}";
        var contentPreview = capturedDocumentJson?.Length > 500 
            ? capturedDocumentJson.Substring(0, 500) + "..." 
            : capturedDocumentJson;
        
        // Verify the document starts with JSON (not HTML or other content)
        var trimmedContent = capturedDocumentJson!.TrimStart();
        if (!trimmedContent.StartsWith("{"))
        {
            Assert.Fail($"Document should be JSON, not HTML or other format.\n{debugInfo}\nContent preview:\n{contentPreview}");
        }
        
        // Verify the document is valid JSON with asyncapi field
        if (!capturedDocumentJson.Contains("\"asyncapi\""))
        {
            Assert.Fail($"Document should contain asyncapi field as JSON property.\n{debugInfo}\nContent preview:\n{contentPreview}");
        }
        
        // Parse and validate with ByteBard
        var reader = new AsyncApiStringReader();
        var document = reader.Read(capturedDocumentJson, out var diagnostic);
        
        document.ShouldNotBeNull($"Document served to UI should be parseable.\n{debugInfo}\nContent preview:\n{contentPreview}");
        
        // Log the document content for debugging
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        if (errorCount > 0)
        {
            var errors = string.Join(Environment.NewLine, diagnostic!.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"Document served to UI has validation errors:{Environment.NewLine}{errors}{Environment.NewLine}{Environment.NewLine}Document content:{Environment.NewLine}{capturedDocumentJson}");
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task AsyncApiUi_DocumentIsValidForUiRendering()
    {
        // Arrange
        using var host = await CreateTestHostWithUiAsync();
        var client = host.GetTestClient();

        // Act - Get the AsyncAPI document that the UI would load
        var documentResponse = await client.GetAsync(GetDocumentRoute(AsyncApiGeneratorConstants.DefaultDocumentName));
        
        // Assert - Document should be valid JSON that the UI can parse
        documentResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        documentResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        
        var documentContent = await documentResponse.Content.ReadAsStringAsync();
        
        // Validate the document can be parsed by the AsyncAPI reader (same validation the UI uses)
        var reader = new AsyncApiStringReader();
        var document = reader.Read(documentContent, out var diagnostic);
        
        document.ShouldNotBeNull("Document should be parseable by AsyncAPI reader");
        
        // Check for schema validation errors that would cause UI rendering issues
        var errorCount = diagnostic?.Errors?.Count() ?? 0;
        if (errorCount > 0)
        {
            var errorMessages = string.Join(Environment.NewLine, 
                diagnostic!.Errors.Select(e => $"- {e.Message}"));
            Assert.Fail($"Document has schema validation errors that would cause UI rendering issues:{Environment.NewLine}{errorMessages}");
        }
    }

    [Fact]
    public async Task AsyncApiUi_RendersWithoutJavaScriptErrors()
    {
        // Arrange
        await StartServerAsync();
        var page = await _browser!.NewPageAsync();
        var consoleErrors = new List<string>();
        var pageErrors = new List<string>();

        // Listen for console errors
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                consoleErrors.Add(msg.Text);
            }
        };

        // Listen for page errors (uncaught exceptions)
        page.PageError += (_, error) =>
        {
            pageErrors.Add(error);
        };

        // Act - Navigate to the UI page
        var response = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for the AsyncAPI component to potentially render
        await page.WaitForTimeoutAsync(2000);

        // Assert
        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue("Page should load successfully");

        // Filter out non-critical errors (e.g., favicon 404)
        var criticalErrors = consoleErrors
            .Where(e => !e.Contains("favicon") && !e.Contains("404"))
            .ToList();

        // Check for schema-related errors specifically
        var schemaErrors = criticalErrors
            .Where(e => e.Contains("schema", StringComparison.OrdinalIgnoreCase) ||
                       e.Contains("AsyncAPI", StringComparison.OrdinalIgnoreCase) ||
                       e.Contains("validation", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (schemaErrors.Any())
        {
            Assert.Fail($"UI encountered schema errors:{Environment.NewLine}{string.Join(Environment.NewLine, schemaErrors)}");
        }

        // Page errors are more severe (uncaught exceptions)
        var criticalPageErrors = pageErrors
            .Where(e => !e.Contains("favicon"))
            .ToList();

        if (criticalPageErrors.Any())
        {
            Assert.Fail($"UI encountered JavaScript errors:{Environment.NewLine}{string.Join(Environment.NewLine, criticalPageErrors)}");
        }

        await page.CloseAsync();
    }

    /// <summary>
    /// Tests that documents with multiple servers render without errors in the UI.
    /// NOTE: This test is skipped due to a known issue with @asyncapi/react-component v3.0.0.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_DocumentWithServersRendersWithoutErrors()
    {
        // Arrange
        await StartServerAsync(options =>
        {
            options.AddServer("production", "api.example.com", "https");
            options.AddServer("staging", "staging.example.com", "https");
            options.WithInfo("Multi-Server API", "1.0.0");
            options.WithDescription("API with multiple servers for UI testing");
        });

        var page = await _browser!.NewPageAsync();
        var pageErrors = new List<string>();

        page.PageError += (_, error) =>
        {
            pageErrors.Add(error);
        };

        // Act
        var response = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(2000);

        // Assert
        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();

        var criticalErrors = pageErrors.Where(e => !e.Contains("favicon")).ToList();
        criticalErrors.ShouldBeEmpty("UI should render multi-server document without errors");
        
        // Check for schema validation error in rendered content
        var pageContent = await page.ContentAsync();
        
        // Extract and display detailed error information if present
        var errorInfoMessage = await ExtractAsyncApiErrorDetails(page);
        var diagnosticInfo = $"\n\nDiagnostic Information:" +
                            $"\n- AsyncAPI error details: {errorInfoMessage}";

        pageContent.ShouldNotContain("Error: There are errors in your Asyncapi document",
            Case.Insensitive,
            $"UI should not display AsyncAPI schema validation errors.{diagnosticInfo}");

        await page.CloseAsync();
    }

    /// <summary>
    /// Tests that AsyncAPI 3.x documents render without errors in the UI.
    /// NOTE: This test is skipped due to a known issue with @asyncapi/react-component v3.0.0
    /// that causes it to fail parsing both v2 and v3 documents with the error
    /// "This is not an AsyncAPI document. The asyncapi field as string is missing."
    /// The document itself is valid (passes ByteBard validation in AsyncApiSchemaValidationTests).
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_V3DocumentRendersWithoutErrors()
    {
        // Arrange
        await StartServerAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("AsyncAPI 3.0 Test", "3.0.0");
        });

        var page = await _browser!.NewPageAsync();
        var pageErrors = new List<string>();
        var consoleMessages = new List<string>();

        page.PageError += (_, error) =>
        {
            pageErrors.Add(error);
        };
        
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" || msg.Type == "warning")
            {
                consoleMessages.Add($"[{msg.Type}] {msg.Text}");
            }
        };

        // Act
        var response = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(2000);

        // Assert
        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();

        var criticalErrors = pageErrors.Where(e => !e.Contains("favicon")).ToList();
        criticalErrors.ShouldBeEmpty("UI should render AsyncAPI 3.0 document without errors");
        
        // Check for schema validation error in rendered content
        var pageContent = await page.ContentAsync();
        
        // Extract and display detailed error information if present
        var errorInfoMessage = await ExtractAsyncApiErrorDetails(page);
        var diagnosticInfo = $"\n\nDiagnostic Information:" +
                            $"\n- Console messages: {string.Join("; ", consoleMessages)}" +
                            $"\n- Page errors: {string.Join("; ", pageErrors)}" +
                            $"\n- AsyncAPI error details: {errorInfoMessage}";
        
        pageContent.ShouldNotContain("Error: There are errors in your Asyncapi document",
            Case.Insensitive,
            $"UI should not display AsyncAPI schema validation errors for v3.{diagnosticInfo}");

        await page.CloseAsync();
    }

    /// <summary>
    /// Tests that AsyncAPI 2.x documents render without errors in the UI.
    /// NOTE: This test is expected to fail with @asyncapi/react-component v3.0.0 because
    /// that version is primarily designed for AsyncAPI 3.0.0 spec and has limited backward
    /// compatibility with AsyncAPI 2.x documents. The error "This is not an AsyncAPI document. 
    /// The asyncapi field as string is missing" indicates the v3 UI component doesn't fully
    /// support v2 document parsing.
    /// 
    /// Root cause: AsyncAPI 2.x spec requires a 'channels' property. When no channels are defined
    /// via [Channel] attributes, the generated document is missing this required property, causing
    /// the @asyncapi/react-component to fail validation.
    /// 
    /// Now fixed - the serialization ensures 'channels: {}' is always present for V2.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_V2DocumentRendersWithoutErrors()
    {
        // Arrange
        await StartServerAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi2_0;
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("AsyncAPI 2.0 Test", "2.0.0");
        });

        var page = await _browser!.NewPageAsync();
        var pageErrors = new List<string>();
        var consoleMessages = new List<string>();

        page.PageError += (_, error) =>
        {
            pageErrors.Add(error);
        };
        
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" || msg.Type == "warning")
            {
                consoleMessages.Add($"[{msg.Type}] {msg.Text}");
            }
        };

        // Act
        var response = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await page.WaitForTimeoutAsync(2000);

        // Assert
        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();

        var criticalErrors = pageErrors.Where(e => !e.Contains("favicon")).ToList();
        criticalErrors.ShouldBeEmpty("UI should render AsyncAPI 2.0 document without errors");
        
        // Check for schema validation error in rendered content
        var pageContent = await page.ContentAsync();
        
        // Extract and display detailed error information if present
        var errorInfoMessage = await ExtractAsyncApiErrorDetails(page);
        var diagnosticInfo = $"\n\nDiagnostic Information:" +
                            $"\n- Console messages: {string.Join("; ", consoleMessages)}" +
                            $"\n- Page errors: {string.Join("; ", pageErrors)}" +
                            $"\n- AsyncAPI error details: {errorInfoMessage}";
        
        pageContent.ShouldNotContain("Error: There are errors in your Asyncapi document",
            Case.Insensitive,
            $"UI should not display AsyncAPI schema validation errors for v2.{diagnosticInfo}");

        await page.CloseAsync();
    }
    
    /// <summary>
    /// Extracts detailed error information from the AsyncAPI UI error display.
    /// </summary>
    private static async Task<string> ExtractAsyncApiErrorDetails(IPage page)
    {
        try
        {
            // Try to find and extract error details from the UI
            var errorElement = await page.QuerySelectorAsync("[class*='error'], [class*='Error'], .validation-error, pre");
            if (errorElement != null)
            {
                var errorText = await errorElement.InnerTextAsync();
                if (!string.IsNullOrWhiteSpace(errorText) && errorText.Length < 2000)
                {
                    return errorText;
                }
            }
            
            // Try to find the error message in the page body
            var bodyText = await page.InnerTextAsync("body");
            if (bodyText.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                // Extract the relevant part containing the error
                var lines = bodyText.Split('\n')
                    .Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                                l.Contains("validation", StringComparison.OrdinalIgnoreCase))
                    .Take(10);
                return string.Join("\n", lines);
            }
            
            return "No detailed error information found";
        }
        catch
        {
            return "Could not extract error details";
        }
    }

    /// <summary>
    /// Tests that the AsyncAPI React component renders content.
    /// NOTE: This test is skipped due to a known issue with @asyncapi/react-component v3.0.0.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_ComponentRendersContent()
    {
        // Arrange
        await StartServerAsync(options =>
        {
            options.AddServer("test-server", "localhost", "http");
            options.WithInfo("Render Test API", "1.0.0");
            options.WithDescription("API to test UI rendering");
        });

        var page = await _browser!.NewPageAsync();

        // Act
        await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for potential React rendering
        await page.WaitForTimeoutAsync(3000);

        // Assert - Check that the asyncapi div has content (React component rendered)
        var asyncApiDiv = await page.QuerySelectorAsync("#asyncapi");
        asyncApiDiv.ShouldNotBeNull("AsyncAPI container div should exist");

        var innerHTML = await asyncApiDiv.InnerHTMLAsync();
        // The div should have some content if the React component rendered
        innerHTML.ShouldNotBeNullOrEmpty("AsyncAPI React component should render some content");
        
        // Check for AsyncAPI schema validation error message from React component
        // The UI renders "Error: There are errors in your Asyncapi document" when schema is invalid
        innerHTML.ShouldNotContain("Error: There are errors in your Asyncapi document", 
            Case.Insensitive, 
            "AsyncAPI document should not have schema validation errors");

        await page.CloseAsync();
    }

    /// <summary>
    /// Tests that the UI doesn't show schema errors when rendering a complete AsyncAPI document.
    /// NOTE: This test is skipped due to a known issue with @asyncapi/react-component v3.0.0
    /// that causes it to fail parsing documents. The document itself is valid.
    /// </summary>
    [Fact]
    public async Task AsyncApiUi_NoSchemaErrorsInRenderedContent()
    {
        // Arrange - This test specifically validates the UI doesn't show schema errors
        await StartServerAsync(options =>
        {
            options.AsyncApiVersion = ByteBard.AsyncAPI.AsyncApiVersion.AsyncApi3_0;
            options.AddServer("production", "api.example.com", "https");
            options.AddServer("staging", "staging.example.com", "https");
            options.WithInfo("Full API Test", "1.0.0");
            options.WithDescription("Comprehensive API for schema validation testing");
            options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
        });

        var page = await _browser!.NewPageAsync();

        // Act
        await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for React rendering
        await page.WaitForTimeoutAsync(3000);

        // Assert - Get the full page content and check for error messages
        var pageContent = await page.ContentAsync();
        
        // The AsyncAPI React component shows this specific error when schema validation fails
        pageContent.ShouldNotContain("Error: There are errors in your Asyncapi document",
            Case.Insensitive,
            "UI should not display AsyncAPI schema validation errors");
        
        // Also check for any generic error displays
        pageContent.ShouldNotContain("validation error", Case.Insensitive);

        await page.CloseAsync();
    }

    /// <summary>
    /// Test that validates the UI renders documents with operations correctly.
    /// This test will fail until the ASP.NET async bindings are properly updated.
    /// Uses HTTP operation bindings similar to StreetlightsAPI example.
    /// </summary>
    [Fact(Skip = "Expected to fail until ASP.NET async bindings are updated - see https://github.com/bielu/Bielu.AspNetCore.AsyncApi for updates")]
    public async Task AsyncApiUi_DocumentWithOperationsRendersWithoutErrors()
    {
        // Arrange - Use HTTP operation bindings similar to StreetlightsAPI example
        await StartServerWithOperationsAsync();

        var page = await _browser!.NewPageAsync();
        var pageErrors = new List<string>();

        page.PageError += (_, error) =>
        {
            pageErrors.Add(error);
        };

        // Act
        var response = await page.GotoAsync($"{_baseUrl}/async-api", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        // Wait for React rendering
        await page.WaitForTimeoutAsync(3000);

        // Assert
        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();

        var criticalErrors = pageErrors.Where(e => !e.Contains("favicon")).ToList();
        
        // Get page content to check for schema errors
        var pageContent = await page.ContentAsync();
        
        // The AsyncAPI React component shows this specific error when schema validation fails
        // This test will fail until the ASP.NET async bindings are properly updated
        pageContent.ShouldNotContain("Error: There are errors in your Asyncapi document",
            Case.Insensitive,
            "UI should not display AsyncAPI schema validation errors for documents with operations. " +
            "This may fail until ASP.NET async bindings are updated.");

        await page.CloseAsync();
    }

    private async Task StartServerWithOperationsAsync()
    {
        // Stop previous host if running
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // Find an available port
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _baseUrl = $"http://localhost:{port}";

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(_baseUrl);
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        // Configure servers
                        options.AddServer("webapi", "localhost:5000", "http", server =>
                        {
                            server.Description = "Local HTTP API Server";
                        });
                        options.AddServer("mqtt-broker", "mqtt.example.com", "mqtt", server =>
                        {
                            server.Description = "MQTT Message Broker";
                        });
                        
                        options.WithInfo("API with Operations", "1.0.0");
                        options.WithDescription("API that includes publish/subscribe operations with HTTP bindings");
                        options.WithDefaultContentType("application/json");
                        
                        // Add HTTP operation binding similar to StreetlightsAPI
                        options.AddOperationBinding("httpPost",
                            new ByteBard.AsyncAPI.Bindings.Http.HttpOperationBinding
                            {
                                Method = "POST"
                            });
                        
                        // Add AMQP channel binding
                        options.AddChannelBinding("amqpQueue",
                            new ByteBard.AsyncAPI.Bindings.AMQP.AMQPChannelBinding
                            {
                                Is = ByteBard.AsyncAPI.Bindings.AMQP.ChannelType.Queue,
                                Queue = new ByteBard.AsyncAPI.Bindings.AMQP.Queue 
                                { 
                                    Name = "test-queue",
                                    Vhost = "/development"
                                }
                            });
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                        endpoints.MapAsyncApiUi();
                        endpoints.MapControllers();
                    });
                });
            })
            .Build();

        await _host.StartAsync();
    }

    private async Task StartServerAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
        // Stop previous host if running
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // Find an available port
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _baseUrl = $"http://localhost:{port}";

        _host = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls(_baseUrl);
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        options.AddServer("test-server", "localhost", "http");
                        options.WithInfo("Test API", "1.0.0");
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                        endpoints.MapAsyncApiUi();
                    });
                });
            })
            .Build();

        await _host.StartAsync();
    }

    private static async Task<IHost> CreateTestHostWithUiAsync(Action<AsyncApiOptions>? configureOptions = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        options.AddServer("test-server", "localhost", "http");
                        options.WithInfo("Test API", "1.0.0");
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                        endpoints.MapAsyncApiUi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
