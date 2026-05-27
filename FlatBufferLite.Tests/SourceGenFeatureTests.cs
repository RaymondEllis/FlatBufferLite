using FlatBufferLite.SourceGen.IR;
using FlatBufferLite.SourceGen.Parsing;
using System.Runtime.InteropServices;

namespace FlatBufferLite.Tests;

public class SourceGenFeatureTests
{
	[Fact]
	public void IncludeDirective_ParsesIncludePaths()
	{
		var source = """
			include "shared.fbs";
			namespace Test;
			table Foo { x: int; }
			root_type Foo;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		Assert.Single(schema.Includes);
		Assert.Equal("shared.fbs", schema.Includes[0]);
	}

	[Fact]
	public void IncludeDirective_ResolvesTypeFromIncludedFile()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "shared.fbs")] = """
				namespace Shared;
				struct Vector3I { x: int; y: int; z: int; }
				""",
			[Path.Combine("test", "main.fbs")] = """
				include "shared.fbs";
				namespace Main;
				table Chunk { pos: Vector3I; }
				root_type Chunk;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "main.fbs"), files);

		Assert.Contains("Vector3I", schema.ByName.Keys);
		Assert.IsType<StructDef>(schema.ByName["Vector3I"]);
		var chunkTable = schema.ByName["Chunk"] as TableDef;
		Assert.NotNull(chunkTable);
		var posField = chunkTable!.Fields[0];
		Assert.Equal("pos", posField.Name);
		Assert.Equal(SchemaBaseType.Obj, posField.Type.Base);
		Assert.Equal("Vector3I", posField.Type.ReferencedName);
	}

	[Fact]
	public void IncludeDirective_TransitiveIncludes()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "base.fbs")] = """
				struct Vec2 { x: float; y: float; }
				""",
			[Path.Combine("test", "mid.fbs")] = """
				include "base.fbs";
				struct Vec3 { x: float; y: float; z: float; }
				""",
			[Path.Combine("test", "top.fbs")] = """
				include "mid.fbs";
				table Thing { pos: Vec3; vel: Vec2; }
				root_type Thing;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "top.fbs"), files);

		Assert.Contains("Vec2", schema.ByName.Keys);
		Assert.Contains("Vec3", schema.ByName.Keys);
		Assert.Contains("Thing", schema.ByName.Keys);
	}

	[Fact]
	public void IncludeDirective_CircularIncludesDoNotLoop()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "a.fbs")] = """
				include "b.fbs";
				struct A { x: int; }
				""",
			[Path.Combine("test", "b.fbs")] = """
				include "a.fbs";
				struct B { y: int; }
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "a.fbs"), files);

		Assert.Contains("A", schema.ByName.Keys);
		Assert.Contains("B", schema.ByName.Keys);
	}

	[Fact]
	public void IncludeDirective_UnresolvedIncludeDoesNotThrow()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "main.fbs")] = """
				include "missing.fbs";
				table Foo { x: int; }
				root_type Foo;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "main.fbs"), files);
		Assert.Single(schema.Tables);
	}

	[Fact]
	public void IncludeDirective_ResolvesRelativeToIncludingFile()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "sub", "color.fbs")] = """
				struct Color { r: ubyte; g: ubyte; b: ubyte; }
				""",
			[Path.Combine("test", "sub", "mid.fbs")] = """
				include "color.fbs";
				struct Material { diffuse: Color; }
				""",
			[Path.Combine("test", "top.fbs")] = """
				include "sub/mid.fbs";
				table Mesh { mat: Material; }
				root_type Mesh;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "top.fbs"), files);

		Assert.Contains("Color", schema.ByName.Keys);
		Assert.Contains("Material", schema.ByName.Keys);
		Assert.Contains("Mesh", schema.ByName.Keys);
	}

	[Fact]
	public void PartialModifier_StructIsEmittedAsPartial()
	{
		var source = """
			struct Vec3 { x: float; y: float; z: float; }
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public partial struct Vec3", code);
		Assert.DoesNotContain("public struct Vec3", code.Replace("public partial struct Vec3", ""));
	}

	[Fact]
	public void MultipleRootTypes_ParserAcceptsMultiple()
	{
		var source = """
			table A { x: int; }
			table B { y: int; }
			root_type A;
			root_type B;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		Assert.Equal(2, schema.RootTypes.Count);
		Assert.Contains("A", schema.RootTypes);
		Assert.Contains("B", schema.RootTypes);
	}

	[Fact]
	public void MultipleRootTypes_AllRootTablesAutoMarkRoot()
	{
		var source = """
			table A { x: int; }
			table B { y: int; }
			root_type A;
			root_type B;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();

		var aIdx = code.IndexOf("public readonly ref partial struct A");
		var bIdx = code.IndexOf("public readonly ref partial struct B");
		Assert.True(aIdx >= 0);
		Assert.True(bIdx >= 0);

		var aSection = code.Substring(aIdx, bIdx - aIdx);
		Assert.Contains("builder.MarkRoot(_pos)", aSection);

		var bSection = code.Substring(bIdx);
		Assert.Contains("builder.MarkRoot(_pos)", bSection);
	}

	[Fact]
	public void MarkAsRoot_EmittedOnEveryTable()
	{
		var source = """
			table A { x: int; }
			table B { y: int; }
			root_type A;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();

		Assert.Contains("public void MarkAsRoot(ref FlatBufferBuilder builder)", code);
		var count = 0;
		var idx = 0;
		while ((idx = code.IndexOf("public void MarkAsRoot(ref FlatBufferBuilder builder)", idx)) >= 0)
		{
			count++;
			idx++;
		}
		Assert.Equal(2, count);
	}

	[Fact]
	public void PascalCase_StructFieldsArePascalCased()
	{
		var source = """
			struct Vec3 { x: float; y: float; z: float; }
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public float X;", code);
		Assert.Contains("public float Y;", code);
		Assert.Contains("public float Z;", code);
		Assert.DoesNotContain("public float x;", code);
		Assert.DoesNotContain("public float y;", code);
		Assert.DoesNotContain("public float z;", code);
	}

	[Fact]
	public void PascalCase_SnakeCaseFieldsConvertedProperly()
	{
		var source = """
			struct Transform { pos_x: float; pos_y: float; scale_factor: float; }
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public float PosX;", code);
		Assert.Contains("public float PosY;", code);
		Assert.Contains("public float ScaleFactor;", code);
	}

	[Fact]
	public void RpcService_ParsedWithoutError()
	{
		var source = """
			namespace MyGame;
			table Monster { hp: int; }
			table MonsterRequest { id: int; }
			table MonsterResponse { name: string; }
			rpc_service MonsterStorage {
			  Store(Monster): MonsterResponse;
			  Retrieve(MonsterRequest): Monster;
			}
			root_type Monster;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		Assert.Equal(3, schema.Tables.Count);
		Assert.Equal("Monster", schema.RootTable);
	}

	[Fact]
	public void NativeInclude_ParsedWithoutError()
	{
		var source = """
			native_include "monster_extra.h";
			table Monster { hp: int; }
			root_type Monster;
			""";
		var parser = new SchemaParser(source);
		var schema = parser.Parse();
		Assert.Single(schema.Tables);
	}

	[Fact]
	public void FieldAttribute_Id_AssignsExplicitVTableSlots()
	{
		var source = """
			table Reordered {
				y: int (id: 1);
				x: int (id: 0);
			}
			root_type Reordered;
			""";
		var schema = new SchemaParser(source).Parse();
		var t = schema.Tables[0];
		var y = t.Fields[0];
		var x = t.Fields[1];
		Assert.Equal(1, y.Id);
		Assert.Equal(0, x.Id);
		Assert.Equal(4 + 1 * 2, y.VTableOffset);
		Assert.Equal(4 + 0 * 2, x.VTableOffset);
		Assert.Equal(2, t.SlotCount);
	}

	[Fact]
	public void FieldAttribute_Required_ParsedAndStored()
	{
		var source = """
			table Strict {
				name: string (required);
				value: int;
			}
			root_type Strict;
			""";
		var schema = new SchemaParser(source).Parse();
		var nameField = schema.Tables[0].Fields[0];
		Assert.Equal("name", nameField.Name);
		Assert.True(nameField.Required);
		Assert.False(schema.Tables[0].Fields[1].Required);
	}

	[Fact]
	public void GetMaxSize_ScalarOnlyEmitsLiteralNoParameters()
	{
		var source = """
			table Simple { x: int; }
			root_type Simple;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public static int GetMaxSize() => ", code);
		Assert.DoesNotContain("TableMaxSize", code);
		Assert.DoesNotContain("public int GetSize()", code);
	}

	[Fact]
	public void GetMaxSize_CoversScalarOnlyBuffer()
	{
		int needed = Sample.Player.GetMaxSize();
		Span<byte> buf = stackalloc byte[needed];
		var b = new FlatBufferBuilder(buf);
		FlatBufferLite.Sample.Player.Create(ref b, id: 1, hp: 50);
		var span = b.Finish();
		Assert.True(needed >= span.Length);
	}

	[Fact]
	public void GetMaxSize_CoversFull_Buffer_WithStringsAndVectors()
	{
		const int nameBytes = 5;
		const int invCount = 3;
		int needed = Sample.Player.GetMaxSize(nameByteCount: nameBytes, inventoryCount: invCount);
		var buf = new byte[needed];
		var b = new FlatBufferBuilder(buf);
		var name = b.CreateString("Alice"u8);
		var inv = b.CreateVector<int>(new[] { 10, 20, 30 });
		FlatBufferLite.Sample.Player.Create(ref b, id: 42, name: name, hp: 250, inventory: inv);
		var bytes = b.Finish();
		Assert.True(needed >= bytes.Length);
	}

	[Fact]
	public void GetMaxSize_EmittedAsStaticMethod()
	{
		var source = """
			table Child { value: int; }
			table WithRefs { name: string; tags: [int]; child: Child; names: [string]; }
			root_type WithRefs;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public static int GetMaxSize(", code);
		Assert.Contains("nameByteCount", code);
		Assert.Contains("tagsCount", code);
		Assert.DoesNotContain("childMaxSize", code);
		Assert.Contains("namesCount", code);
		Assert.Contains("namesByteCount", code);
		Assert.Contains("nameByteCount + 8", code);
		Assert.Contains("tagsCount * 4 + 7", code);
		Assert.Contains("namesByteCount + namesCount * 8", code);
		Assert.DoesNotContain("FlatBufferBuilder.StringMaxSize", code);
		Assert.DoesNotContain("FlatBufferBuilder.VectorMaxSize", code);
		Assert.DoesNotContain("FlatBufferBuilder.VectorOfOffsetsMaxSize", code);
	}

	[Fact]
	public void FixedNestedTable_GetMaxSize_DoesNotEmitPayloadMaxSize()
	{
		var source = """
			table Score { value: long; }
			table Player { score: Score; }
			root_type Player;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.DoesNotContain("scoreMaxSize", code);
	}

	[Fact]
	public void VariableNestedTable_GetMaxSize_EmitsPayloadMaxSize()
	{
		var source = """
			table Child { name: string; }
			table Parent { child: Child; }
			root_type Parent;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("childMaxSize", code);
	}

	[Fact]
	public void FixedRefUnion_GetMaxSize_DoesNotEmitPayloadMaxSize()
	{
		var source = """
			table Circle { radius: float; }
			union Shape { Circle }
			table WithShape { shape: Shape; }
			root_type WithShape;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
			Assert.DoesNotContain("shapeMaxSize", code);
		}

		[Fact]
		public void VariableRefUnion_GetMaxSize_EmitsPayloadMaxSize()
		{
			var source = """
				table Circle { name: string; }
				union Shape { Circle }
				table WithShape { shape: Shape; }
				root_type WithShape;
				""";
			var schema = new SchemaParser(source).Parse();
			var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
			Assert.Contains("shapeMaxSize", code);
	}

	[Fact]
	public void FieldAttribute_Required_AlwaysWritten()
	{
		var source = """
			table Doc { content: string (required); }
			root_type Doc;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.DoesNotContain("if (content != 0)", code);
		Assert.Contains("WriteOffset", code);
	}

	[Fact]
	public void FieldAttribute_Required_RuntimeEnforced()
	{
		var buf = new byte[256];
		var b = new FlatBufferBuilder(buf);
		bool threw = false;
		try
		{
			FlatBufferLite.Req.Doc.Create(ref b, title: default);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}
		Assert.True(threw, "Expected InvalidOperationException for required field with offset 0.");
	}

	[Fact]
	public void FieldAttribute_Key_ParsedAndStored()
	{
		var source = """
			table Entry { id: int (key); name: string; }
			root_type Entry;
			""";
		var schema = new SchemaParser(source).Parse();
		var idField = schema.Tables[0].Fields[0];
		Assert.True(idField.IsKey);
		Assert.False(schema.Tables[0].Fields[1].IsKey);
	}

	[Fact]
	public void FieldAttribute_Key_LookupByKeyEmitted()
	{
		var source = """
			table Entry { id: int (key); name: string; }
			root_type Entry;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public Entry LookupByKey(int key)", code);
	}

	[Fact]
	public void FieldAttribute_Key_StringLookupByKeyEmitted()
	{
		var source = """
			table Item { name: string (key); }
			root_type Item;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public Item LookupByKey(ReadOnlySpan<byte> key)", code);
	}

	[Fact]
	public void FieldAttribute_Key_ScalarLookupByKey_RoundTrips()
	{
		Span<byte> buf = stackalloc byte[1024];
		var b = new FlatBufferBuilder(buf);

		var n1 = b.CreateString("one"u8);
		var n2 = b.CreateString("two"u8);
		var n3 = b.CreateString("three"u8);

		var e1 = FlatBufferLite.Attr.Entry.Create(ref b, id: 10, name: n1);
		var e2 = FlatBufferLite.Attr.Entry.Create(ref b, id: 20, name: n2);
		var e3 = FlatBufferLite.Attr.Entry.Create(ref b, id: 30, name: n3);

		ReadOnlySpan<int> offsets = stackalloc int[] { e1.BufferPos, e2.BufferPos, e3.BufferPos };
		var vec = b.CreateVectorOfOffsets(offsets);

		var outer = FlatBufferLite.Attr.Inner.Create(ref b, value: 0);
		_ = b.Finish();

		var v = new FlatBufferLite.Attr.EntryVector(buf, vec);
		Assert.Equal(3, v.Length);

		var found = v.LookupByKey(20);
		Assert.True(found.IsValid);
		Assert.Equal(20, found.Id);
		Assert.Equal("two", found.Name.ToString());

		var missing = v.LookupByKey(99);
		Assert.False(missing.IsValid);
	}

	[Fact]
	public void FieldAttribute_Key_ScalarLookupByKey_UnsortedCreation()
	{
		Span<byte> buf = stackalloc byte[1024];
		var b = new FlatBufferBuilder(buf);

		var n1 = b.CreateString("thirty"u8);
		var n2 = b.CreateString("ten"u8);
		var n3 = b.CreateString("twenty"u8);

		var e1 = FlatBufferLite.Attr.Entry.Create(ref b, id: 30, name: n1);
		var e2 = FlatBufferLite.Attr.Entry.Create(ref b, id: 10, name: n2);
		var e3 = FlatBufferLite.Attr.Entry.Create(ref b, id: 20, name: n3);

		// Store in sorted key order for binary search
		ReadOnlySpan<int> offsets = stackalloc int[] { e2.BufferPos, e3.BufferPos, e1.BufferPos };
		var vec = b.CreateVectorOfOffsets(offsets);

		var outer = FlatBufferLite.Attr.Inner.Create(ref b, value: 0);
		_ = b.Finish();

		var v = new FlatBufferLite.Attr.EntryVector(buf, vec);
		Assert.Equal(3, v.Length);

		var found10 = v.LookupByKey(10);
		Assert.True(found10.IsValid);
		Assert.Equal(10, found10.Id);
		Assert.Equal("ten", found10.Name.ToString());

		var found30 = v.LookupByKey(30);
		Assert.True(found30.IsValid);
		Assert.Equal(30, found30.Id);
		Assert.Equal("thirty", found30.Name.ToString());

		var missing = v.LookupByKey(15);
		Assert.False(missing.IsValid);
	}

	[Fact]
	public void FieldAttribute_Hash_ParsedAndStored()
	{
		var source = """
			table Hashed { tag: string (hash: "fnv1a_32"); }
			root_type Hashed;
			""";
		var schema = new SchemaParser(source).Parse();
		Assert.Equal("fnv1a_32", schema.Tables[0].Fields[0].HashAlgorithm);
	}

	[Fact]
	public void FieldAttribute_NestedFlatbuffer_ParsedAndStored()
	{
		var source = """
			table Inner { value: int; }
			table Outer { blob: [ubyte] (nested_flatbuffer: "Inner"); }
			root_type Outer;
			""";
		var schema = new SchemaParser(source).Parse();
		Assert.Equal("Inner", schema.Tables[1].Fields[0].NestedFlatBufferType);
	}

	[Fact]
	public void FieldAttribute_NestedFlatbuffer_AccessorEmitted()
	{
		var source = """
			table Inner { value: int; }
			table Outer { blob: [ubyte] (nested_flatbuffer: "Inner"); }
			root_type Outer;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("BlobNested", code);
		Assert.Contains("Inner.GetRootAs(Blob.AsSpan)", code);
	}

	[Fact]
	public void FieldAttribute_NestedFlatbuffer_RoundTrips()
	{
		Span<byte> innerBuf = stackalloc byte[128];
		var ib = new FlatBufferBuilder(innerBuf);
		FlatBufferLite.Attr.Inner.Create(ref ib, value: 42);
		var innerBytes = ib.Finish();

		Span<byte> outerBuf = stackalloc byte[512];
		var ob = new FlatBufferBuilder(outerBuf);
		var blob = ob.CreateVector<byte>(MemoryMarshal.Cast<byte, byte>(innerBytes));
		FlatBufferLite.Attr.Outer.Create(ref ob, blob: blob);
		var outerBytes = ob.Finish();

		var o = Attr.Outer.GetRootAs(outerBytes);
		var nested = o.BlobNested;
		Assert.True(nested.IsValid);
		Assert.Equal(42, nested.Value);
	}

	[Fact]
	public void FieldAttribute_Flexbuffer_ParsedAndStored()
	{
		var source = """
			table Flex { data: [ubyte] (flexbuffer); }
			root_type Flex;
			""";
		var schema = new SchemaParser(source).Parse();
		Assert.True(schema.Tables[0].Fields[0].IsFlexBuffer);
	}

	[Fact]
	public void TypeAttribute_OriginalOrder_ParsedAndStored()
	{
		var source = """
			table Ordered (original_order) { b: int; a: int; }
			root_type Ordered;
			""";
		var schema = new SchemaParser(source).Parse();
		Assert.True(schema.Tables[0].OriginalOrder);
	}

	[Fact]
	public void TypeAttribute_ForceAlign_ChangesStructAlignment()
	{
		var source = """
			struct Aligned (force_align: 16) { x: float; y: float; z: float; }
			""";
		var schema = new SchemaParser(source).Parse();
		var s = schema.Structs[0];
		Assert.Equal(16, s.ForceAlign);
		Assert.Equal(16, s.Alignment);
		Assert.Equal(16, s.Size);
	}

	[Fact]
	public void FieldAttribute_ForceAlign_ChangesStructFieldAlignment()
	{
		var source = """
			struct Padded { a: byte (force_align: 4); b: int; }
			""";
		var schema = new SchemaParser(source).Parse();
		var s = schema.Structs[0];
		Assert.Equal(4, s.Fields[0].ForceAlign);
		Assert.Equal(0, s.Fields[0].Offset);
		Assert.Equal(4, s.Fields[1].Offset);
	}

	// --- Include path resolution ---

	[Fact]
	public void IncludeDirective_ForwardSlashKeys_ResolvedCorrectly()
	{
		// fileContents uses forward-slash paths (cross-platform style).
		// On Windows, ParseWithIncludes normalises the entry path to backslashes,
		// so Path.Combine(dir, include) also produces backslashes.
		// The dictionary lookup then fails because the key still has forward slashes
		// → the include appears "missing" even though it is present.
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["test/shared.fbs"] = "struct Vector3I { x: int; y: int; z: int; }",
			["test/main.fbs"] = """
				include "shared.fbs";
				table Chunk { pos: Vector3I; }
				root_type Chunk;
				""",
		};

		var missingIncludes = new List<string>();
		var schema = SchemaParser.ParseWithIncludes("test/main.fbs", files, missingIncludes);

		Assert.Empty(missingIncludes);
		Assert.Contains("Vector3I", schema.ByName.Keys);
	}

	// --- Missing include: layout corruption ---

	[Fact]
	public void MissingInclude_UnresolvedStructField_AddsSchemaWarning()
	{
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "main.fbs")] = """
				include "numerics.fbs";
				table Chunk { pos: Vector3I; data: [ubyte]; }
				root_type Chunk;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "main.fbs"), files);

		Assert.Contains(schema.Warnings, w => w.Contains("Vector3I"));
	}

	// --- Missing include: codegen corruption ---

	[Fact]
	public void MissingInclude_StructField_CreateParamUsesStructType()
	{
		// When Vector3I is not resolved, BuildParamType falls through to "int".
		// The generated Create() should still use the struct type name so that the
		// caller gets a compile-time error rather than silently accepting an int.
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "main.fbs")] = """
				include "numerics.fbs";
				table Chunk { pos: Vector3I; }
				root_type Chunk;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "main.fbs"), files);
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();

		Assert.Contains("Vector3I pos", code);
		Assert.DoesNotContain(", int pos ", code);
		Assert.DoesNotContain(", int pos)", code);
	}

	[Fact]
	public void MissingInclude_StructField_BuildAssignIsEmitted()
	{
		// When Vector3I is not resolved, EmitBuildAssign silently generates nothing
		// for the pos field.  The serialisation is dropped without any diagnostic.
		var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[Path.Combine("test", "main.fbs")] = """
				include "numerics.fbs";
				table Chunk { pos: Vector3I; }
				root_type Chunk;
				""",
		};

		var schema = SchemaParser.ParseWithIncludes(Path.Combine("test", "main.fbs"), files);
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();

		// The Create(ref FlatBufferBuilder builder, …) body must contain an actual
		// Vtable write for pos, not just the parameter declaration.
		int createIdx = code.IndexOf("Create(ref FlatBufferBuilder builder,");
		Assert.True(createIdx >= 0, "Create overload not found");
		int createEnd = code.IndexOf("\n\t}", createIdx);
		var createBody = code.Substring(createIdx, createEnd - createIdx);
		Assert.Contains("Vtable.Write", createBody);
	}

	[Fact]
	public void ContiguousUnion_AllStructMembers_EmittedAsValueType()
	{
		var source = """
			struct Point { x: int; y: int; }
			struct Size { w: int; h: int; }
			union PointOrSize { Point, Size }
			table Spot { shape: PointOrSize; }
			root_type Spot;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public struct PointOrSize : IUnion", code);
		Assert.DoesNotContain("ref struct PointOrSize", code);
		Assert.Contains("public readonly bool TryGetValue(out Point value)", code);
		Assert.Contains("public readonly bool TryGetValue(out Size value)", code);
		Assert.Contains("public static PointOrSize FromPoint(Point value)", code);
		Assert.Contains("public static PointOrSize FromSize(Size value)", code);
	}

	[Fact]
	public void OpaqueUnion_MixedStructAndTable_EmittedWithoutTryGetAs()
	{
		var source = """
			struct Point { x: int; y: int; }
			table Circle { r: float; }
			union Mixed { Point, Circle }
			table Scene { shape: Mixed; }
			root_type Scene;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("public readonly ref struct Mixed : IUnion", code);
		Assert.DoesNotContain("TryGetAs", code);
	}

	[Fact]
	public void VectorOfEnum_EmittedAsFlatVector()
	{
		var source = """
			enum Dir : ubyte { N = 0, S = 1, E = 2, W = 3 }
			table Path { steps: [Dir]; }
			root_type Path;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("FlatVector<", code);
		Assert.Contains("Steps", code);
	}

	[Fact]
	public void VectorOfFixedTables_GetMaxSize_EmitsCountOnly()
	{
		var source = """
			table Item { value: int; }
			table Bag { items: [Item]; }
			root_type Bag;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("itemsCount", code);
		Assert.DoesNotContain("itemsMaxSize", code);
		Assert.DoesNotContain("itemsMaxSizeEach", code);
	}

	[Fact]
	public void VectorOfVariableTables_GetMaxSize_EmitsCountAndTotalPayloadSize()
	{
		var source = """
			table Item { name: string; }
			table Bag { items: [Item]; }
			root_type Bag;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("itemsCount", code);
		Assert.Contains("itemsMaxSize", code);
		Assert.DoesNotContain("itemsMaxSizeEach", code);
	}

	[Fact]
	public void EnumDefault_TableFieldGetter_UsesSchemaDefault()
	{
		var source = """
			enum Color : ubyte { Red = 0, Green = 1, Blue = 2 }
			table Widget { tint: Color = Green; }
			root_type Widget;
			""";
		var schema = new SchemaParser(source).Parse();
		var code = new SourceGen.Emit.CodeEmitter(schema).Emit();
		Assert.Contains("Color.Green", code);
	}
}