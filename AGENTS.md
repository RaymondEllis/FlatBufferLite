# Kings orders

- Prefer no code comments. If you write a comment think about if that code should be rewritten to be more clear instead.
- Comments are allowed if they explain why something is done, but not how. The code should be clear enough to explain how it works without comments. Or if the comment is future looking or providing extra context.
- All code is C#.
- Do not reply with syntax fixes.
- Do not write "Clean Code" this codebase prioritizes performance and low-level optimizations.
- Look for race conditions and threading issues in multithreaded code.
- Be aware of allocations, GC is bad.
- `unsafe` code is allowed, only when necessary for performance.
- We do not write standard .net code, we write high performance code.
- Prefer fields over properties, and structs over classes, unless reference semantics are required. The performance difference with properties is significant and we should use structs to be more aware of allocations.
- Target .NET 10, Windows and Linux, best if we can be fully cross platform and no native libs.
- KISS

## Docs

### FlatBuffers
https://flatbuffers.dev/

### C# Unions
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union
As of .NET 10 we need our own interfaces to match the coming union support.
