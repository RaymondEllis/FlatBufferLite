# FlatBufferLite

> **Disclaimer:** This library was written by AI and almost certainly contains flaws, bugs, and design oversights. The API may change completely in the next commit. Use at your own risk.

A lightweight, zero-allocation, high-performance [FlatBuffers](https://flatbuffers.dev/) implementation in C# targeting game development. The goal is a minimal runtime with no heap allocations on the hot path, using `ref struct`, `Span<T>`, and source-generated table types — no reflection, no boxing, no surprises.

---

## Generated Type Naming

Every `table` in your schema generates a **ref struct** (`readonly ref partial struct`) named `{Name}Ref`. When annotated with `(plain_struct)`, an additional regular C# struct named `{Name}` is generated alongside the ref struct.

### Ref structs vs Plain structs

**Ref structs** (`PlayerRef`) are zero-allocation wrappers over a `Span<byte>` buffer. They read fields directly from the flat binary data with no copies, no GC pressure, and no heap usage. They cannot escape the stack and cannot be stored in class fields or collections.

**Plain structs** (`Player`) are regular C# value-type DTOs with public fields. They copy data out of the buffer into managed memory. Strings become `byte[]`, vectors become arrays, and nested tables become nullable plain structs. Use plain structs when you need to store deserialized data beyond the lifetime of the buffer, or pass it across async boundaries.

| Aspect | Ref struct (`PlayerRef`) | Plain struct (`Player`) |
| --- | --- | --- |
| Heap allocation | Zero | Arrays/strings allocated |
| Storage | Stack only (`Span<T>` rules) | Anywhere (fields, collections) |
| Read performance | Direct buffer access, O(1) | One-time copy cost |
| Mutability | Setter writes back to buffer | Regular mutable fields |
| Use case | Hot-path reading/writing | Long-lived deserialized data |

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
| Type attribute: `plain_struct` (tables) → plain C# struct DTO | ✅ |
| Type attribute: `original_order` (tables) | ✅ (preserves declaration order; default table layout packs by alignment/size) |
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
PlayerRef.Create(ref b, id: 42, name: name, hp: 250, status: Status.Pending, inventory: inv);

ReadOnlySpan<byte> bytes = b.Finish();
```

You can also use the reserve constructor and set fields individually:

```csharp
var pb = PlayerRef.Create(ref b);
pb.Id = 42;
pb.Hp = 250;

ReadOnlySpan<byte> bytes = b.Finish();
```

### Pre-allocating buffers with `GetMaxSize`

Every generated table has one sizing method: `GetMaxSize(...)`. Use it to size the initial buffer before writing. Tables with only fixed-size fields have no parameters, so the generated method returns a literal. Fixed-size nested tables and fixed-size ref union payloads are folded into that generated size. Dynamic strings, vectors, or variable nested payloads accept only the counts or byte totals needed to calculate the upper bound.

```csharp
Span<byte> buf = stackalloc byte[PlayerRef.GetMaxSize()];
var b = new FlatBufferBuilder(buf);
PlayerRef.Create(ref b, id: 1, hp: 100);

int bufSize = PlayerRef.GetMaxSize(nameByteCount: 5, inventoryCount: 3);
var buf2 = new byte[bufSize];
var b2 = new FlatBufferBuilder(buf2);
int name = b2.CreateString("Alice"u8);
int inv  = b2.CreateVector<int>(new[] { 10, 20, 30 });
PlayerRef.Create(ref b2, id: 42, name: name, hp: 250, inventory: inv);
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
int bufSize = RefsRef.GetMaxSize(strValByteCount: 11);
```

Variable nested payloads compose through the same method:

```csharp
int bufSize = MonsterRef.GetMaxSize(
    loadoutMaxSize: LoadoutRef.GetMaxSize(labelByteCount: 8));
```

For a vector of fixed-size tables, only the vector count is needed. For a vector of variable-size tables, also pass the total max-size budget for all elements:

```csharp
int weaponBytes = WeaponRef.GetMaxSize(nameByteCount: 5)
    + WeaponRef.GetMaxSize(nameByteCount: 4);

int bufSize = MonsterRef.GetMaxSize(
    nameByteCount: 6,
    weaponsCount: 2,
    weaponsMaxSize: weaponBytes);
```

Fixed-size ref union payloads are included automatically. Variable ref union payloads use a `MaxSize` parameter for the selected payload:

```csharp
int bufSize = WithUnionRef.GetMaxSize(
    nameByteCount: 9,
    tagByteCount: 11);

int bufSizeWithVariableShape = WithUnionRef.GetMaxSize(
    nameByteCount: 9,
    shapeMaxSize: NamedShapeRef.GetMaxSize(labelByteCount: 6),
    tagByteCount: 11);
```

---

## Reading

```csharp
var player = PlayerRef.GetRootAs(bytes);

int   id   = player.Id;           // 42
short hp   = player.Hp;           // 250
string name = player.Name.ToString(); // "Alice"

ReadOnlySpan<int> inventory = player.Inventory.AsSpan;
```

For types that are not the schema root, read directly from the builder buffer using the position returned by the constructor:

```csharp
var score = ScoreRef.Create(ref b, value: 9_876_543_210L);
var read  = new ScoreRef(b.Buffer, score.BufferPos);

long v = read.Value; // 9_876_543_210
```

---

## Plain Structs

Annotate a table with `(plain_struct)` to also generate a regular C# struct with public fields and `Serialize` / `Deserialize` helpers:

```fbs
attribute "plain_struct";

table Player (plain_struct) {
  id: int;
  name: string;
  inventory: [int];
}
```

This emits a `Player` plain struct alongside the zero-allocation `PlayerRef` ref struct. Plain structs can be used at the root or nested inside other plain structs when the nested table is also annotated. FlatBuffers structs and enums can be fields directly.

Strings in plain structs use UTF-8 byte arrays (`byte[]`), and vectors use arrays, so serializing or deserializing those fields allocates on the managed heap.

```csharp
// Serialize from plain struct
var player = new Player { Id = 42, Name = "Alice"u8.ToArray(), Inventory = new[] { 10, 20, 30 } };
Span<byte> buf = stackalloc byte[4096];
var b = new FlatBufferBuilder(buf);
Player.Serialize(ref b, in player);
var bytes = b.Finish();

// Deserialize to plain struct
var read = new Player();
Player.Deserialize(bytes, ref read);
```

### Custom Collections

For plain structs, fields can be annotated with `(CustomCollection)` to use poolable collection interfaces instead of arrays, avoiding per-frame allocations when deserializing repeatedly:

```fbs
attribute "plain_struct";
attribute "CustomCollection";

table Bag (plain_struct) {
  scores: [int] (CustomCollection);
  names: [string] (CustomCollection);
  items: [Item] (CustomCollection);
}
```

This generates fields typed as `IFlatBufferCollection<T>` (for scalars, structs, enums) or `IFlatBufferPlainVector<T>` (for strings and nested tables). You must register factory functions before deserializing:

```csharp
FlatBufferCollections<int>.Create = capacity => new MyIntCollection(capacity);
FlatBufferPlainVectors<Item>.Create = capacity => new MyItemVector(capacity);
```

The deserializer reuses existing collection instances via `ReplaceRange` / `Resize` — no new allocations on repeated deserialize calls.

---

## Vectors

Vectors of scalar types use `FlatVector<T>` which provides zero-copy `AsSpan` access. Vectors of tables use generated `{Name}RefVector` types with indexed access. Vectors of strings use `FlatStringVector`.

```csharp
// Scalar vector — zero-copy span access
FlatVector<int> inventory = player.Inventory;
ReadOnlySpan<int> span = inventory.AsSpan;

// Table vector — indexed access
WeaponRefVector weapons = monster.Weapons;
WeaponRef first = weapons[0];

// String vector
FlatStringVector names = bag.Names;
FlatString firstName = names[0];
ReadOnlySpan<byte> utf8 = firstName.AsBytes;
```

### LookupByKey

Fields annotated with `(key)` generate a binary-search `LookupByKey` method on the vector type:

```csharp
var entry = entries.LookupByKey(targetId);
if (entry.IsValid) { /* found */ }
```

---

## Unions

Unions with only struct/enum members generate as contiguous value-type unions (explicit layout struct). Unions containing table members generate as `readonly ref struct` with `TryGetAs{Member}` methods:

```csharp
// Contiguous union (all-struct members)
var shape = PointOrSize.FromPoint(new Point { X = 1, Y = 2 });
if (shape.TryGetValue(out Point p)) { /* use p */ }

// Ref union (contains tables)
if (scene.Shape.TryGetAsCircle(out var circle))
    float r = circle.R;
```

---

## Performance Notes

`GetMaxSize()` is generated as straight integer arithmetic. For fixed-size tables it returns a literal; for dynamic data it uses the byte counts, element counts, and nested payload budgets you pass. Calls with literal arguments are allocation-free and can be folded by the JIT.
