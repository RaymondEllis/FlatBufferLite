# FlatBufferLite

> **Disclaimer:** This library was written by AI and almost certainly contains flaws, bugs, and design oversights. The API may change completely in the next commit. Use at your own risk.

A lightweight, zero-allocation, high-performance [FlatBuffers](https://flatbuffers.dev/) implementation in C# targeting game development. The goal is a minimal runtime with no heap allocations on the hot path, using `ref struct`, `Span<T>`, and source-generated table types — no reflection, no boxing, no surprises.

---

## Schema Support

The source generator parses `.fbs` files and supports the following FlatBuffers schema features:

| Feature | Status |
| --- | --- |
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
| Type attribute: `native_struct` (tables) → plain C# struct DTO | ✅ |
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

Every generated table has one sizing method: `GetMaxSize(...)`. Use it to size the initial buffer before writing. Tables with only fixed-size fields have no parameters, so the generated method returns a literal. Fixed-size nested tables and fixed-size ref union payloads are folded into that generated size. Dynamic strings, vectors, or variable nested payloads accept only the counts or byte totals needed to calculate the upper bound.

```csharp
Span<byte> buf = stackalloc byte[Player.GetMaxSize()];
var b = new FlatBufferBuilder(buf);
new Player(ref b, id: 1, hp: 100);

int bufSize = Player.GetMaxSize(nameByteCount: 5, inventoryCount: 3);
var buf2 = new byte[bufSize];
var b2 = new FlatBufferBuilder(buf2);
int name = b2.CreateString("Alice"u8);
int inv  = b2.CreateVector<int>(new[] { 10, 20, 30 });
new Player(ref b2, id: 42, name: name, hp: 250, inventory: inv);
ReadOnlySpan<byte> bytes = b2.Finish();
```

Generated parameter suffixes are deliberately mechanical:

| Suffix | Meaning |
| --- | --- |
| `ByteCount` | Total UTF-8 bytes for a string field, or all strings in a string vector |
| `Count` | Number of vector elements |
| `MaxSize` | Total max-size budget for a variable nested table, ref union payload, or all variable table/ref-union vector elements |

Fixed-size nested payloads are included automatically:

```csharp
int bufSize = Refs.GetMaxSize(strValByteCount: 11);
```

Variable nested payloads compose through the same method:

```csharp
int bufSize = Monster.GetMaxSize(
    loadoutMaxSize: Loadout.GetMaxSize(labelByteCount: 8));
```

For a vector of fixed-size tables, only the vector count is needed. For a vector of variable-size tables, also pass the total max-size budget for all elements:

```csharp
int weaponBytes = Weapon.GetMaxSize(nameByteCount: 5)
    + Weapon.GetMaxSize(nameByteCount: 4);

int bufSize = Monster.GetMaxSize(
    nameByteCount: 6,
    weaponsCount: 2,
    weaponsMaxSize: weaponBytes);
```

Fixed-size ref union payloads are included automatically. Variable ref union payloads use a `MaxSize` parameter for the selected payload:

```csharp
int bufSize = WithUnion.GetMaxSize(
    nameByteCount: 9,
    tagByteCount: 11);

int bufSizeWithVariableShape = WithUnion.GetMaxSize(
    nameByteCount: 9,
    shapeMaxSize: NamedShape.GetMaxSize(labelByteCount: 6),
    tagByteCount: 11);
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

### Native structs

Annotate a table with `(native_struct)` to also generate a regular C# struct DTO with public fields and `Serialize` / `Deserialize` helpers:

```fbs
attribute "native_struct";

table Player (native_struct) {
  id: int;
  name: string;
  inventory: [int];
}
```

This emits `PlayerNative` alongside the zero-allocation `Player` ref struct. Native structs can be used at the root or nested inside other native structs when the nested table is also annotated. FlatBuffers structs and enums can be fields directly.

Strings in native structs use UTF-8 byte arrays (`byte[]`), and vectors use arrays, so serializing or deserializing those fields allocates on the managed heap.

## Performance Notes

`GetMaxSize()` is generated as straight integer arithmetic. For fixed-size tables it returns a literal; for dynamic data it uses the byte counts, element counts, and nested payload budgets you pass. Calls with literal arguments are allocation-free and can be folded by the JIT.
