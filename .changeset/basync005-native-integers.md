---
"bielu-aspnetcore-asyncapi": patch
---

The `BASYNC005` analyzer now reports `IntPtr` and `UIntPtr` used as a message payload, message headers or channel parameter type. It compared the type's display name with `"System.IntPtr"`, but since C# 11 these are the same types as `nint` and `nuint` and display as such, so they were never reported.
