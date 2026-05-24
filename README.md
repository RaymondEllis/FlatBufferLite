> **Disclaimer:** This library was written by AI and almost certainly contains flaws, bugs, and design oversights. The API may change completely in the next commit. Use at your own risk.

# FlatBufferLite

A lightweight, zero-allocation, high-performance [FlatBuffers](https://flatbuffers.dev/) implementation in C# targeting game development. The goal is a minimal runtime with no heap allocations on the hot path, using `ref struct`, `Span<T>`, and source-generated table types — no reflection, no boxing, no surprises.

---

## Schema Support

The source generator parses `.fbs` files and supports the following FlatBuffers schema features:

| Feature | Status |
|---|---|
| `namespace` | ✅ |
| `table` | ✅ |
| `struct` | ✅ |
| `enum` | ✅ |
| `union` | ✅ |
| `root_type` (multiple) | ✅ |
| `file_identifier` | ✅ |
| `file_extension` | ✅ |
| `include` / `native_include` | ✅ |
| `rpc_service` (parsed, no code gen) | ✅ |
| `attribute` declarations | ✅ |
| Field attribute: `deprecated` | ✅ |
| Field attribute: `id` (explicit vtable slot) | ✅ |
| Field attribute: `required` | ✅ (parsed) |
| Field attribute: `key`, `hash`, `nested_flatbuffer`, `flexbuffer`, `force_align` | ✅ (parsed, ignored) |
| Type attribute: `original_order`, `force_align` | ✅ (parsed, ignored) |
| Enum attribute: `bit_flags` | ✅ |
| Scalar types: all (`bool`, `byte`/`int8`, `ubyte`/`uint8`, `short`/`int16`, `ushort`/`uint16`, `int`/`int32`, `uint`/`uint32`, `long`/`int64`, `ulong`/`uint64`, `float`/`float32`, `double`/`float64`) | ✅ |
| Vector types | ✅ |
| Nested tables, structs, enums in fields | ✅ |

---

## Writing

Define your schema (`.fbs`) and the source generator emits typed constructors. Strings, vectors, and nested tables must be created before the table that references them.

```csharp
Span<byte> buf = stackalloc byte[512];
var b = new FlatBufferBuilder(buf);

int name = b.CreateString("Alice"u8);
int inv  = b.CreateVector<int>(stackalloc int[] { 10, 20, 30 });

// Build constructor: all fields in one call, elides fields equal to their schema default.
new Player(ref b, id: 42, name: name, hp: 250, status: Status.Pending, inventory: inv);

ReadOnlySpan<byte> bytes = b.AsSpan(); // root offset is written here
```

You can also use the reserve constructor and set fields individually:

```csharp
var pb = new Player(ref b);
pb.Id = 42;
pb.Hp = 250;

ReadOnlySpan<byte> bytes = b.AsSpan();
```

### Pre-allocating buffers with `GetMaxSize`

`GetMaxSize()` is a source-generated static method that returns a compile-time constant — the maximum number of bytes needed to write exactly one instance of the table as a root (vtable + table data + root offset + worst-case alignment padding, excluding variable-length fields such as strings and vectors).

```csharp
Span<byte> buf = stackalloc byte[Player.GetMaxSize()];
var b = new FlatBufferBuilder(buf);
new Player(ref b, id: 1, hp: 100);
ReadOnlySpan<byte> bytes = b.AsSpan();
```

---

## Reading

```csharp
var player = Player.GetRootAs(bytes);

int   id   = player.Id;           // 42
short hp   = player.Hp;           // 250
string name = player.Name.ToString(); // "Alice"

ReadOnlySpan<int> inventory = player.Inventory.AsSpan;
```

For types that are not the schema root, read directly from the builder buffer using the position returned by the constructor:

```csharp
var score = new Score(ref b, value: 9_876_543_210L);
var read  = new Score(b.Buffer, score.Pos);

long v = read.Value; // 9_876_543_210
```

### Measuring size with `GetSize`

`GetSize()` is a source-generated instance method that reads the vtable to return the exact number of bytes the table's structural data occupies (vtable + table data area, not counting the payloads of referenced strings, vectors, or nested tables).

```csharp
var player = Player.GetRootAs(bytes);
int structural = player.GetSize(); // vtable + table data bytes
int budget     = Player.GetMaxSize(); // upper bound including root + alignment
```

`GetMaxSize()` is a pure constant (zero memory reads) while `GetSize()` traverses the vtable, making `GetMaxSize()` the right choice for buffer pre-allocation.

---

## Benchmarks

The `FlatBufferLite.Benchmarks` project demonstrates the cost difference:

```
| Method     | Mean      | Allocated |
|----------- |----------:|----------:|
| GetMaxSize | 0.000 ns  |       0 B |
| GetSize    | 1.234 ns  |       0 B |
```
*(example output — actual numbers depend on hardware and runtime)*

`GetMaxSize()` is folded to a constant by the JIT with no memory access. `GetSize()` requires three reads from the buffer (soffset, vtableSize, tableDataSize).

Run benchmarks:

```
dotnet run --project FlatBufferLite.Benchmarks -c Release
```
