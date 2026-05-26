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
| `file_identifier` | ⚠️ (parsed; use `builder.MarkRoot(pos, "ABCD"u8)` manually) |
| `file_extension` | ❌ (parsed, no effect) |
| `include` / `native_include` | ✅ |
| `rpc_service` | ❌ (emits compiler warning FBL003, no code gen) |
| `attribute` declarations | ✅ (parsed, allows unknown attributes) |
| Field attribute: `deprecated` | ✅ |
| Field attribute: `id` (explicit vtable slot) | ✅ |
| Field attribute: `required` | ✅ (write-time exception if required reference field is null) |
| Field attribute: `key` → `LookupByKey` on vector | ✅ |
| Field attribute: `hash` | ❌ (parsed only; hashing not applied) |
| Field attribute: `nested_flatbuffer` → typed `XxxNested` accessor | ✅ |
| Field attribute: `flexbuffer` | ❌ (parsed only; FlexBuffers not supported) |
| Field attribute: `force_align` (struct fields) | ✅ |
| Type attribute: `force_align` (structs) | ✅ |
| Type attribute: `original_order` (tables) | ❌ (parsed only; table fields are always in declaration order) |
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

`TableMaxSize` is a source-generated `const int` for the fixed-size portion of the table (vtable + inline fields + root offset + worst-case alignment). For tables with strings or vectors, use `GetMaxSize(...)` which accepts the byte counts and element counts of those fields:

```csharp
// Only scalar/struct fields — TableMaxSize is sufficient:
Span<byte> buf = stackalloc byte[Player.TableMaxSize];
var b = new FlatBufferBuilder(buf);
new Player(ref b, id: 1, hp: 100);

// With strings and vectors — use GetMaxSize:
int bufSize = Player.GetMaxSize(nameByteCount: 5, inventoryCount: 3);
var buf2 = new byte[bufSize];
var b2 = new FlatBufferBuilder(buf2);
int name = b2.CreateString("Alice"u8);
int inv  = b2.CreateVector<int>(new[] { 10, 20, 30 });
new Player(ref b2, id: 42, name: name, hp: 250, inventory: inv);
ReadOnlySpan<byte> bytes = b2.AsSpan();
```

When a table has a vector-of-table field (e.g. `weapons: [Weapon]`), pass the per-element upper bound via `weaponsMaxSizeEach`:

```csharp
int bufSize = Monster.GetMaxSize(
    nameByteCount: 6,
    weaponsCount: 2,
    weaponsMaxSizeEach: Weapon.GetMaxSize(nameByteCount: 5));
```

The generated expression covers both the offset array and each element's data:
`VectorOfOffsetsMaxSize(weaponsCount) + weaponsCount * weaponsMaxSizeEach`

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
