using System;

namespace InfiniteTool.Extensions
{
    static class StringExtensions
	{
		public static string DeCamelCase(this string original)
		{
			var potentialSpaces = 0;

			for (int i = 0; i < original.Length; i++)
			{
				var c = original[i];

				if (char.IsUpper(c))
				{
					potentialSpaces++;
				}
			}

			if (potentialSpaces == 0)
			{
				return original;
			}

			var outputLength = original.Length + potentialSpaces;
			Span<char> output = stackalloc char[0];

			if (outputLength < 500)
			{
				output = stackalloc char[outputLength];
			}
			else
			{
				output = new char[outputLength];
			}

			var outputLocation = 0;
			for (int i = 0; i < original.Length; i++)
			{
				var c = original[i];

				output[outputLocation++] = c;

				if (i < original.Length - 1)
				{
					var n = original[i + 1];

					if (char.IsLower(c) && char.IsUpper(n))
					{
						output[outputLocation++] = ' ';
					}
					else if (i < original.Length - 2)
					{
						var n2 = original[i + 2];

						if (char.IsUpper(c) && char.IsUpper(n) && char.IsLower(n2))
						{
							output[outputLocation++] = ' ';
						}
					}
				}
			}

			return new string(output.Slice(0, outputLocation));
		}
	}
}
