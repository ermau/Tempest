//
// ISerializer.cs
//
// Author:
//   Eric Maupin <me@ermau.com>
//
// Copyright (c) 2011 Eric Maupin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Tempest
{
	/// <summary>
	/// Contract for a type that serializes another type.
	/// </summary>
	/// <typeparam name="T">The type to serialize and deserialize.</typeparam>
	public interface ISerializer<T>
	{
		/// <summary>
		/// Serializes <paramref name="element"/> using <paramref name="writer"/>.
		/// </summary>
		/// <param name="element">The element to serialize.</param>
		/// <param name="writer">The writer to use to serialize.</param>
		void Serialize (T element, IValueWriter writer);

		/// <summary>
		/// Deserializes an element with <paramref name="reader"/>.
		/// </summary>
		/// <param name="reader">The reader to use to deserialize.</param>
		/// <returns>The deserialized element.</returns>
		T Deserialize (IValueReader reader);
	}

	public static class Serializer<T>
	{
		public static readonly ISerializer<T> Default = new DefaultSerializer();

		private class DefaultSerializer
			: ISerializer<T>
		{
			public void Serialize (T element, IValueWriter writer)
			{
				ObjectSerializer.GetSerializer (element.GetType()).Serialize (writer, element);
			}

			public T Deserialize (IValueReader reader)
			{
				return (T)ObjectSerializer.GetSerializer (typeof (T)).Deserialize (reader);
			}
		}
	}
}