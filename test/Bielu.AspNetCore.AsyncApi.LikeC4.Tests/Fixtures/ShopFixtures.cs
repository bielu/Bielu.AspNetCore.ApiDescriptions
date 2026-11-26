using Shouldly;
using Xunit;
namespace Bielu.AspNetCore.AsyncApi.LikeC4.Tests.Fixtures;

/// <summary>
/// The documents of a small shop: orders (3.0) sends orders it takes and receives reservations, inventory (3.0)
/// receives orders and sends reservations and stock changes, notifications (2.6, YAML) receives stock changes.
/// </summary>
internal static class ShopFixtures
{
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    /// <summary>The golden files, in the source tree, so <c>BIELU_UPDATE_GOLDEN=1</c> rewrites them there.</summary>
    public static string GoldenDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "Golden");

    public static string Read(string fileName) => File.ReadAllText(Path.Combine(Directory, fileName));

    public static IReadOnlyList<AsyncApiLikeC4Source> Sources() =>
    [
        new("orders", Read("orders.asyncapi.json")) { Link = "https://orders.example.com/asyncapi/v1.json" },
        new("inventory", Read("inventory.asyncapi.json")),
        new("notifications", Read("notifications.asyncapi.yaml")),
    ];

    /// <summary>Compares <paramref name="actual"/> with a golden file, or rewrites it under <c>BIELU_UPDATE_GOLDEN=1</c>.</summary>
    public static void ShouldMatchGolden(string actual, string goldenFileName)
    {
        var path = Path.Combine(GoldenDirectory(), goldenFileName);
        if (Environment.GetEnvironmentVariable("BIELU_UPDATE_GOLDEN") == "1")
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            return;
        }

        File.Exists(path).ShouldBeTrue($"Missing golden file {path}. Run with BIELU_UPDATE_GOLDEN=1 to create it.");
        actual.ShouldBe(File.ReadAllText(path).ReplaceLineEndings("\n"));
    }
}
