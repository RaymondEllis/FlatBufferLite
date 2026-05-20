> **Disclaimer:** This library was written by AI and almost certainly contains flaws, bugs, and design oversights. The API may change completely in the next commit. Use at your own risk.

# FlatBufferLite

A lightweight, zero-allocation, high-performance [FlatBuffers](https://flatbuffers.dev/) implementation in C# targeting game development. The goal is a minimal runtime with no heap allocations on the hot path, using `ref struct`, `Span<T>`, and source-generated table types — no reflection, no boxing, no surprises.

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
