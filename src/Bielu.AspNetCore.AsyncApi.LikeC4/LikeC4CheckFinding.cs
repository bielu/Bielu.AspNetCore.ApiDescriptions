namespace Bielu.AspNetCore.AsyncApi.LikeC4;

/// <summary>How much a <see cref="LikeC4CheckFinding"/> matters.</summary>
public enum LikeC4CheckSeverity
{
    /// <summary>The model and the AsyncAPI documents disagree.</summary>
    Error,

    /// <summary>Something the model does not cover, which may be intended.</summary>
    Warning,
}

/// <summary>A place where a LikeC4 model and the AsyncAPI documents it describes disagree.</summary>
/// <param name="Severity">How much it matters.</param>
/// <param name="Message">What disagrees, in a sentence.</param>
/// <param name="Location">
/// Where: <c>element &lt;id&gt;</c>, <c>relation &lt;id&gt;</c> or <c>source &lt;name&gt;</c>; <see langword="null"/>
/// when the model has nothing to point at, such as a relationship it lacks.
/// </param>
public sealed record LikeC4CheckFinding(LikeC4CheckSeverity Severity, string Message, string? Location);
