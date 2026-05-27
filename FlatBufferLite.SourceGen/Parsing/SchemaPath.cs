using System.Collections.Generic;

namespace FlatBufferLite.SourceGen.Parsing;

static class SchemaPath
{
	public static string Normalize(string path)
	{
		path = path.Replace('\\', '/');
		var rootLength = GetRootLength(path);
		List<string>? segments = null;
		var changed = false;
		var segmentStart = rootLength;
		while (segmentStart <= path.Length)
		{
			var nextSlash = segmentStart < path.Length ? path.IndexOf('/', segmentStart) : -1;
			var segmentEnd = nextSlash >= 0 ? nextSlash : path.Length;
			var segmentLength = segmentEnd - segmentStart;

			if (segmentLength == 0)
			{
				changed |= nextSlash >= 0;
			}
			else
			{
				var segment = path.Substring(segmentStart, segmentLength);
				if (segment == ".")
				{
					changed = true;
				}
				else if (segment == "..")
				{
					segments ??= new List<string>();
					if (segments.Count > 0 && segments[segments.Count - 1] != "..")
						segments.RemoveAt(segments.Count - 1);
					else if (rootLength == 0)
						segments.Add(segment);
					changed = true;
				}
				else
				{
					segments ??= new List<string>();
					segments.Add(segment);
				}
			}

			if (nextSlash < 0)
				break;
			segmentStart = nextSlash + 1;
		}

		if (!changed)
			return path;

		segments ??= new List<string>();
		var prefix = rootLength > 0 ? path.Substring(0, rootLength) : "";
		if (segments.Count == 0)
			return prefix;
		if (prefix.Length == 0 || prefix[prefix.Length - 1] == '/')
			return prefix + string.Join("/", segments);
		return prefix + "/" + string.Join("/", segments);
	}

	static int GetRootLength(string path)
	{
		if (path.Length == 0)
			return 0;
		if (path[0] == '/')
		{
			if (path.Length < 2 || path[1] != '/')
				return 1;

			var serverEnd = path.IndexOf('/', 2);
			if (serverEnd < 0)
				return path.Length;
			var shareEnd = path.IndexOf('/', serverEnd + 1);
			return shareEnd < 0 ? path.Length : shareEnd + 1;
		}
		if (path.Length > 1 && path[1] == ':')
			return path.Length > 2 && path[2] == '/' ? 3 : 2;
		return 0;
	}
}