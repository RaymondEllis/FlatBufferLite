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
		var sharedSource = """
			namespace Shared;
			struct Vector3I { x: int; y: int; z: int; }
			""";
		var mainSource = """
			include "shared.fbs";
			namespace Main;
			table Chunk { pos: Vector3I; }
			root_type Chunk;
			""";

		var schema = SchemaParser.ParseWithIncludes(mainSource, path =>
		{
			if (path == "shared.fbs") return sharedSource;
			return null;
		});

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
		var baseSource = """
			struct Vec2 { x: float; y: float; }
			""";
		var midSource = """
			include "base.fbs";
			struct Vec3 { x: float; y: float; z: float; }
			""";
		var topSource = """
			include "mid.fbs";
			table Thing { pos: Vec3; vel: Vec2; }
			root_type Thing;
			""";

		var schema = SchemaParser.ParseWithIncludes(topSource, path => path switch
		{
			"mid.fbs" => midSource,
			"base.fbs" => baseSource,
			_ => null,
		});

		Assert.Contains("Vec2", schema.ByName.Keys);
		Assert.Contains("Vec3", schema.ByName.Keys);
		Assert.Contains("Thing", schema.ByName.Keys);
	}

	[Fact]
	public void IncludeDirective_CircularIncludesDoNotLoop()
	{
		var aSource = """
			include "b.fbs";
			struct A { x: int; }
			""";
		var bSource = """
			include "a.fbs";
			struct B { y: int; }
			""";

		var schema = SchemaParser.ParseWithIncludes(aSource, path => path switch
		{
			"b.fbs" => bSource,
			"a.fbs" => aSource,
			_ => null,
		});

		Assert.Contains("A", schema.ByName.Keys);
		Assert.Contains("B", schema.ByName.Keys);
	}

	[Fact]
	public void IncludeDirective_UnresolvedIncludeDoesNotThrow()
	{
		var source = """
			include "missing.fbs";
			table Foo { x: int; }
			root_type Foo;
			""";

		var schema = SchemaParser.ParseWithIncludes(source, _ => null);
		Assert.Single(schema.Tables);
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

		var aIdx = code.IndexOf("public readonly ref struct A");
		var bIdx = code.IndexOf("public readonly ref struct B");
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
}
