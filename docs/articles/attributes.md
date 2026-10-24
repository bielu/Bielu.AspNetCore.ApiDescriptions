# Attributes

Bielu.AspNetCore.AsyncApi uses attributes to discover and describe your AsyncAPI components.

## Main Attributes

| Attribute | Description |
|-----------|-------------|
| `[AsyncApi]` | Marks a class for scanning by the generator. Without this, other attributes on the class or its methods will be ignored. |
| `[Channel]` | Defines a channel. Can be applied to a class or a method. |
| `[SendOperation]` | Defines an operation through which the application sends messages (AsyncAPI 3 `action: send`; a `subscribe` operation in 2.x). |
| `[ReceiveOperation]` | Defines an operation through which the application receives messages (AsyncAPI 3 `action: receive`; a `publish` operation in 2.x). |
| `[Message]` | Provides metadata for a message, such as its name, summary, or headers. |
| `[MessageExample]` | Defines an example for a message. |
| `[ChannelParameter]` | Defines a parameter in a channel path (e.g., `{id}`). |

`[PublishOperation]` and `[SubscribeOperation]` still work but are obsolete. They were named the AsyncAPI 2.x way, from the client's point of view, so they read backwards next to AsyncAPI 3 output: `[PublishOperation]` is replaced by `[ReceiveOperation]`, and `[SubscribeOperation]` by `[SendOperation]`.

## Usage Examples

### Channel and Operations

```csharp
[AsyncApi]
public class MyMessageBus
{
    [Channel("users/{userId}/signed-up")]
    [ChannelParameter("userId", Description = "The unique user ID")]
    [SendOperation(typeof(UserSignedUp), "UserSignUp", Summary = "Emitted when a user signs up.")]
    public void HandleUserSignUp(UserSignedUp msg) { }
}
```

### Message Metadata

You can use the `[Message]` attribute to customize how a message appears in the document.

```csharp
[Message(Name = "UserEvent", Summary = "A generic user event")]
public class UserEvent { ... }
```

On a class the payload type is the class itself, so no type argument is needed. The values become the
defaults for every channel that uses the class as a payload. A `[Message]` on a method still names the
payload and overrides whatever it sets:

```csharp
[Channel("users/created")]
[Message(typeof(UserEvent), Title = "User created")] // Name and Summary come from the class
public void UserCreated() { }
```

The source generator picks up class-level `[Message]` attributes too, so the same document is produced
under Native AOT.

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
