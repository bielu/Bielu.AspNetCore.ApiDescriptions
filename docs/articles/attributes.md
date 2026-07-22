# Attributes

Bielu.AspNetCore.AsyncApi uses attributes to discover and describe your AsyncAPI components.

## Main Attributes

| Attribute | Description |
|-----------|-------------|
| `[AsyncApi]` | Marks a class for scanning by the generator. Without this, other attributes on the class or its methods will be ignored. |
| `[Channel]` | Defines a channel. Can be applied to a class or a method. |
| `[SubscribeOperation]` | Defines a subscribe operation (server publishes, client receives). |
| `[PublishOperation]` | Defines a publish operation (client publishes, server receives). |
| `[Message]` | Provides metadata for a message, such as its name, summary, or headers. |
| `[MessageExample]` | Defines an example for a message. |
| `[ChannelParameter]` | Defines a parameter in a channel path (e.g., `{id}`). |

## Usage Examples

### Channel and Operations

```csharp
[AsyncApi]
public class MyMessageBus
{
    [Channel("users/{userId}/signed-up")]
    [ChannelParameter("userId", Description = "The unique user ID")]
    [SubscribeOperation(typeof(UserSignedUp), "UserSignUp", Summary = "Emitted when a user signs up.")]
    public void HandleUserSignUp(UserSignedUp msg) { }
}
```

### Message Metadata

You can use the `[Message]` attribute to customize how a message appears in the document.

```csharp
[Message(Name = "UserEvent", Summary = "A generic user event")]
public class UserEvent { ... }
```

### Message Examples

Examples can be defined as JSON literals or via a provider class.

```csharp
[MessageExample("Basic", Json = "{\"id\": 123, \"name\": \"John\"}")]
public class UserSignedUp { ... }
```

Or using a provider:

```csharp
[MessageExample(typeof(MyExampleProvider))]
public class UserSignedUp { ... }

public class MyExampleProvider : IAsyncApiMessageExampleProvider
{
    public object GetExample() => new UserSignedUp { Id = 123, Name = "John" };
}
```

## XML Documentation Support

If enabled, the generator will also use your C# XML documentation comments (`/// <summary>`, `/// <remarks>`) as fallbacks for descriptions if the attributes don't provide them.

See [Configuration](configuration.md) to enable XML comments.
