using FlatBufferLite.SourceGen.IR;

namespace FlatBufferLite.SourceGen.Emit;

public sealed partial class CodeEmitter
{
void EmitReserveConstructor(TableDef t)
{
_w.Append("public static ").Append(t.Name).Append("Ref").AppendLine(" Create(ref FlatBufferBuilder builder)");
_w.OpenBlock();
_w.Append("int __pos = builder.StartTable(").Append(t.SlotCount).Append(", ").Append(t.InlineSize).Append(", ").Append(t.InlineAlign).AppendLine(");");
_w.AppendLine("var __buf = builder.Buffer;");
foreach (var f in t.Fields)
{
if (f.Deprecated)
continue;
EmitFieldAssign(f, forced: true);
}
_w.AppendLine("builder.MarkRoot(__pos);");
_w.Append("return new ").Append(t.Name).Append("Ref").AppendLine("(__buf, __pos);");
_w.CloseBlock();
}

void EmitFieldAssign(FieldDef f, bool forced)
{
int absInline = f.InlineOffset + 4;
int vto = f.VTableOffset;
string pname = ToCamelCase(f.Name);
if (f.Type.IsUnion)
{
if (forced)
{
_w.Append("Vtable.WriteForced<byte>(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", 0);");
}
else
{
int dataVto = vto + 2;
int dataAbsInline = f.UnionDataInlineOffset + 4;
_w.Append("Vtable.Write<byte>(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", (byte)").Append(pname).Append("Type, 0);");
_w.AppendLine();
_w.Append("if (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(dataVto).Append(", ").Append(dataAbsInline).Append(", ").Append(pname).AppendLine(");");
}
return;
}
if (f.Type.Base.IsScalar())
{
string cs = ScalarCSharpType(f.Type, out string defLit);
string schemaDefault = !string.IsNullOrEmpty(f.DefaultValue) ? FormatDefault(f.Type, f.DefaultValue!) : defLit;
if (forced)
_w.Append("Vtable.WriteForced<").Append(cs).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(schemaDefault).AppendLine(");");
else
_w.Append("Vtable.Write<").Append(cs).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).Append(", ").Append(schemaDefault).AppendLine(");");
return;
}
if (f.Type.IsString || f.Type.IsVector)
{
if (!forced)
{
if (f.Required)
_w.Append("Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
else
_w.Append("if (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
}
return;
}
if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
{
if (def is TableDef)
{
if (!forced)
{
if (f.Required)
_w.Append("Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
else
_w.Append("if (").Append(pname).Append(" != 0) Vtable.WriteOffset(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(pname).AppendLine(");");
}
return;
}
if (def is StructDef)
{
if (forced)
_w.Append("{ var __v = default(").Append(f.Type.ReferencedName).Append("); Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
else
_w.Append("{ var __v = ").Append(pname).Append("; Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
return;
}
if (def is EnumDef ed)
{
string under = ed.Underlying.ToCSharpKeyword();
string defValue = !string.IsNullOrEmpty(f.DefaultValue)
? FormatEnumDefault(ed, f.DefaultValue!)
: (ed.Underlying == SchemaBaseType.Long || ed.Underlying == SchemaBaseType.ULong ? "0L" : "0");
if (forced)
_w.Append("Vtable.WriteForced<").Append(under).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", ").Append(defValue).AppendLine(");");
else
_w.Append("Vtable.Write<").Append(under).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).Append(", (").Append(under).Append(')').Append(pname).Append(", ").Append(defValue).AppendLine(");");
return;
}
}
else if (f.Type.IsObject && f.Type.ReferencedName != null)
{
if (forced)
_w.Append("{ var __v = default(").Append(f.Type.ReferencedName).Append("); Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
else
_w.Append("{ var __v = ").Append(pname).Append("; Vtable.WriteForced<").Append(f.Type.ReferencedName).Append(">(__buf, __pos, ").Append(vto).Append(", ").Append(absInline).AppendLine(", in __v); }");
}
}

void EmitBuildConstructor(TableDef t)
{
_w.Append("public static ").Append(t.Name).Append("Ref").Append(" Create(ref FlatBufferBuilder builder");
foreach (var f in t.Fields)
{
if (f.Deprecated)
continue;
if (f.Type.IsUnion)
{
_w.Append(", ").Append(f.Type.ReferencedName).Append("Kind ").Append(ToCamelCase(f.Name)).Append("Type = default");
_w.Append(", int ").Append(ToCamelCase(f.Name)).Append(" = 0");
continue;
}
_w.Append(", ").Append(BuildParamType(f)).Append(' ').Append(ToCamelCase(f.Name)).Append(" = ").Append(BuildParamDefault(f));
}
_w.AppendLine(")");
_w.OpenBlock();
_w.Append("int __pos = builder.StartTable(").Append(t.SlotCount).Append(", ").Append(t.InlineSize).Append(", ").Append(t.InlineAlign).AppendLine(");");
_w.AppendLine("var __buf = builder.Buffer;");
foreach (var f in t.Fields)
{
if (f.Deprecated)
continue;
EmitFieldAssign(f, forced: false);
}
_w.AppendLine("builder.MarkRoot(__pos);");
_w.Append("return new ").Append(t.Name).Append("Ref").AppendLine("(__buf, __pos);");
_w.CloseBlock();
}

string BuildParamType(FieldDef f)
{
if (f.Type.Base.IsScalar())
return ScalarCSharpType(f.Type, out _);
if (f.Type.IsString)
return "StringOffset";
if (f.Type.IsVector)
return "VectorOffset";
if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
{
if (def is TableDef)
return "Offset<" + f.Type.ReferencedName + "Ref>";
if (def is StructDef)
return f.Type.ReferencedName;
if (def is EnumDef)
return f.Type.ReferencedName;
}
return f.Type.ReferencedName ?? "int";
}

string BuildParamDefault(FieldDef f)
{
if (f.Type.Base.IsScalar())
{
ScalarCSharpType(f.Type, out string defLit);
return !string.IsNullOrEmpty(f.DefaultValue) ? FormatDefault(f.Type, f.DefaultValue!) : defLit;
}
if (f.Type.IsString || f.Type.IsVector)
return "default";
if (f.Type.IsObject && f.Type.ReferencedName != null && _schema.ByName.TryGetValue(f.Type.ReferencedName, out var def))
{
if (def is TableDef)
return "default";
if (def is StructDef)
return "default";
if (def is EnumDef ed2)
{
if (!string.IsNullOrEmpty(f.DefaultValue))
{
if (f.DefaultValue!.Length > 0 && (char.IsDigit(f.DefaultValue[0]) || f.DefaultValue[0] == '-'))
return "(" + ed2.Name + ")" + f.DefaultValue;
return ed2.Name + "." + f.DefaultValue;
}
return "default";
}
}
return f.Type.IsObject && f.Type.ReferencedName != null ? "default" : "0";
}
}
