# FlatBufferLite

> **Disclaimer:** This library was written by AI and almost certainly contains flaws, bugs, and design oversights. The API may change completely in the next commit. Use at your own risk — no surprises, except the ones we haven't found yet.

A lightweight, zero-allocation, high-performance [FlatBuffers](https://flatbuffers.dev/) implementation in C# for game development. No heap allocations on the hot path, no reflection, no boxing. Types are source-generated from `.fbs` schemas.

---

## Generated Types

See the [FlatBuffers documentation](https://flatbuffers.dev/) for schema syntax. Every `table` generates a **ref struct** named `{Name}Ref`. Annotate with `(plain_struct)` to also generate a regular C# struct named `{Name}`.

The primary type is the ref struct. Plain structs exist for cases where you need to outlive the buffer.

| | Ref struct (`PlayerRef`) | Plain struct (`Player`) |
| --- | --- | --- |
| Heap allocation | Zero | Heap per string/vector/nested table |
| Additional storage | None — reads/writes directly into the buffer | Full copy of all fields |
| Buffer lifetime | Must stay in scope | Independent of buffer |
| Stack/field storage | Stack only (`ref struct` rules) | Anywhere — fields, lists, async |
| Mutability | Setters write back into the buffer | Regular mutable fields |

---

## Schema Support

| Feature | Status |
| --- | --- |
| `namespace`, `table`, `struct`, `enum`, `union` | ✅ |
| `root_type` (multiple) | ✅ |
| `include` / `native_include` | ✅ |
| `file_identifier` | ⚠️ Parsed; use `builder.MarkRoot(pos, "ABCD"u8)` manually |
| `file_extension` | ❌ No effect |
| `rpc_service` | ❌ Emits warning FBL003, no code gen |
| Field: `deprecated`, `id`, `required`, `key`, `nested_flatbuffer`, `flexbuffer`, `force_align` | ✅ |
| Field: `hash` | ❌ Parsed only |
| Type: `plain_struct`, `original_order`, `force_align` | ✅ |
| Enum: `bit_flags` | ✅ |
| All scalar types, vectors, nested types | ✅ |

---

## Writing

Strings, vectors, and nested tables must be created before the table that references them.

```csharp
Span<byte> buf = stackalloc byte[512];
var b = new FlatBufferBuilder(buf);

int name = b.CreateString("Alice"u8);
int inv  = b.CreateVector<int>(stackalloc int[] { 10, 20, 30 });

PlayerRef.Create(ref b, id: 42, name: name, hp: 250, inventory: inv);

ReadOnlySpan<byte> bytes = b.Finish();
```

Or set scalar and fixed-size struct fields individually after creation. **All string, vector, and nested table values must still be created before calling `Create`** — the reserve constructor immediately claims buffer space, so any `CreateString`/`CreateVector` call after it would be placed below the table's reserved region and cannot be referenced by it.

```csharp
int name = b.CreateString("Alice"u8);  // must come first
var pb = PlayerRef.Create(ref b);
pb.Id = 42;          // scalar — fine
pb.Hp = 250;         // scalar — fine
// pb.Name = name;   // also fine, offset was created before Create()
ReadOnlySpan<byte> bytes = b.Finish();
```

### Pre-allocating with `GetMaxSize`

Every generated table has `GetMaxSize(...)` to calculate an exact upper-bound buffer size. Pass `0` for dynamic fields you are not writing.

```csharp
// Fixed-size only — no parameters needed
Span<byte> buf = stackalloc byte[PlayerRef.GetMaxSize()];

// With dynamic fields
int bufSize = PlayerRef.GetMaxSize(nameByteCount: 5, inventoryCount: 3);
Span<byte> buf = stackalloc byte[bufSize];
```

| Parameter suffix | Meaning |
| --- | --- |
| `ByteCount` | UTF-8 bytes for a string field, or total for all strings in a string vector |
| `Count` | Number of vector elements |
| `MaxSize` | Max-size budget for a variable nested table, ref union payload, or all elements in a variable vector |

Nested table sizes compose:

```csharp
WeaponSize w1 = WeaponRef.GetMaxSize(nameByteCount: 5);
WeaponSize w2 = WeaponRef.GetMaxSize(nameByteCount: 4);

int bufSize = MonsterRef.GetMaxSize(
    nameByteCount: 6,
    weaponsCount: 2,
    weaponsMaxSize: new WeaponSize(w1.Value + w2.Value));
```

---

## Reading

```csharp
var player = PlayerRef.GetRootAs(bytes);

int   id        = player.Id;
short hp        = player.Hp;
FlatString name = player.Name;

ReadOnlySpan<int> inventory = player.Inventory.AsSpan;
```

For non-root types, use the buffer position returned by the constructor:

```csharp
var score = ScoreRef.Create(ref b, value: 9_876_543_210L);
var read  = new ScoreRef(b.Buffer, score.BufferPos);
```

---

## Vectors

- Scalar vectors: `.AsSpan` for zero-copy access
- Table vectors: indexed by `[i]`
- String vectors: indexed by `[i]`, returns `FlatString` with `.AsBytes`

Fields annotated with `(key)` generate a binary-search `LookupByKey` method:

```csharp
var entry = entries.LookupByKey(targetId);
if (entry.IsValid) { /* found */ }
```

---

## Unions

- **All struct/enum members** → value-type union with `TryGetValue`
- **Contains table members** → `readonly ref struct` with `TryGetAs{Member}` methods

```csharp
// Value union
var shape = PointOrSize.FromPoint(new Point { X = 1, Y = 2 });
if (shape.TryGetValue(out Point p)) { ... }

// Ref union
if (scene.Shape.TryGetAsCircle(out var circle)) { ... }
```

---

## Plain Structs

Annotate a table with `(plain_struct)` to generate a regular C# struct with `Serialize` / `Deserialize` helpers. Strings and vector fields allocate on the heap.

```fbs
table Player (plain_struct) {
  id: int;
  name: string;
  inventory: [int];
}
```

```csharp
var player = new Player { Id = 42, Name = "Alice"u8.ToArray(), Inventory = new[] { 10, 20, 30 } };
Span<byte> buf = stackalloc byte[4096];
var b = new FlatBufferBuilder(buf);
Player.Serialize(ref b, in player);
var bytes = b.Finish();

var read = new Player();
Player.Deserialize(bytes, ref read);
```

### Custom Collections

Annotate vector fields with `(CustomCollection)` to replace the default array fields with `IFlatBufferCollection<T>` or `IFlatBufferPlainVector<T>`. This lets you supply your own collection type — including pooled ones — which can eliminate per-deserialize allocations if your implementation supports it. Register factories before use:

```csharp
FlatBufferCollections<int>.Create = capacity => new MyIntCollection(capacity);
FlatBufferPlainVectors<Item>.Create = capacity => new MyItemVector(capacity);
```

---

## FlexBuffers

Schema-less values can be stored in `[ubyte] (flexbuffer)` fields. The source generator emits an `XxxFlexBuffer` accessor alongside the raw byte-vector accessor. Supported value kinds: null, bool, int, uint, float, string, blob, vector.

```fbs
table Event {
  payload: [ubyte] (flexbuffer);
}
```

```csharp
Span<byte> flexBuf = stackalloc byte[256];
var fb = new FlexBufferBuilder(flexBuf);
var values = fb.CreateVector(stackalloc FlexBufferValue[]
{
    FlexBufferValue.Int(42),
    fb.CreateString("spawn"u8),
    FlexBufferValue.Bool(true),
});
VectorOffset payload = builder.CreateVector<byte>(fb.Finish(values));
EventRef.Create(ref builder, payload: payload);

long first = EventRef.GetRootAs(bytes).PayloadFlexBuffer.AsVector[0].AsInt64;
```
