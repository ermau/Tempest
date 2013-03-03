using System.Collections;
using System.Linq;

namespace NUnit.Framework
{
	internal class CollectionAssert
	{
		public static void Contains (IEnumerable collection, object actual)
		{
			Assert.IsTrue (collection.Cast<object>().Contains (actual), "Collection did not contain {0}", actual);
		}

		public static void IsNotEmpty (IEnumerable enumerable)
		{
			ICollection collection = enumerable as ICollection;
			if (collection != null)
			{
				Assert.IsTrue (collection.Count > 0, "Collection was empty");
				return;
			}

			var enumerator = enumerable.GetEnumerator();
			Assert.IsTrue (enumerator.MoveNext(), "Collection was empty");
		}

		public static void AreEqual (IEnumerable e1, IEnumerable e2)
		{
			IEnumerator t1 = e1.GetEnumerator();
			IEnumerator t2 = e2.GetEnumerator();

			while (true)
			{
				bool m1 = t1.MoveNext();
				bool m2 = t2.MoveNext();
				Assert.AreEqual (m1, m2, "Collection lengths did not match");

				if (!m1 || !m2)
					return;

				Assert.AreEqual (t1.Current, t2.Current);
			}
		}
	}
}