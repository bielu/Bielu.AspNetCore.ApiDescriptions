#:sdk Microsoft.NET.Sdk.Web

// The API the Aspire container test points Scalar at, run by Aspire as a file-based app: it serves an OpenAPI and an
// Arazzo document to the Scalar container, and the built Arazzo plugin module (PLUGIN_MODULE) to the browser, with
// CORS so the module can be imported from Scalar's origin.
var builder = WebApplication.CreateSlimBuilder(args);
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.AccessControlAllowOrigin = "*";
    await next();
});

app.MapGet("/openapi/v1.json", () => Results.Text("""
    { "openapi": "3.1.0", "info": { "title": "Shop", "version": "1.0.0" },
      "paths": { "/orders": { "post": { "operationId": "createOrder", "responses": { "201": { "description": "Created" } } } } } }
    """, "application/json"));

// A document whose Pages view appears only with the options set, under names nobody would guess.
app.MapGet("/arazzo/v1.json", () => Results.Text("""
    {
      "arazzo": "1.1.0",
      "info": { "title": "Shop", "version": "1.0.0" },
      "x-test-pages": { "checkout": { "title": "Checkout page", "route": "/checkout" } },
      "sourceDescriptions": [{ "name": "shop", "url": "/openapi/v1.json", "type": "openapi" }],
      "workflows": [{
        "workflowId": "placeOrder",
        "steps": [{
          "stepId": "create", "operationId": "createOrder", "x-test-page": "checkout", "x-test-label": "Place the order",
          "onFailure": [{ "name": "declined", "type": "end", "x-test-end": "Declined page" }]
        }]
      }]
    }
    """, "application/json"));

app.MapGet("/plugins/scalar-plugin.mjs", () => Results.File(Environment.GetEnvironmentVariable("PLUGIN_MODULE")!, "text/javascript"));

app.Run();
