using Microsoft.VisualStudio.TestTools.UnitTesting;
using HackPDM.ClientUtils;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using static HackPDM.ClientUtils.Tests.TestInfo;
using static System.Net.Mime.MediaTypeNames;

namespace HackPDM.ClientUtils.Tests
{
	[TestClass()]
	public class ExtMethodsTests
	{
		[TestMethod()]
		public void SelectTest()
		{
			ArrayList list = StaticTestInfo.testInfo.arrListTest;
			int[] strLengths = list.Select<string, int>(str => str.Length).ToArray();

			Assert.AreEqual( 3, strLengths [ 0 ] );
			Assert.AreEqual( 14, strLengths [ 1 ] );
		}

		[TestMethod()]
		public void SelectTest1()
		{
			Hashtable hashtable = StaticTestInfo.testInfo.hashTableTest;
			IEnumerable<int> strLengths = hashtable.Select<string, int>(str => str.Length);

			Assert.IsTrue ( strLengths.Contains( 6 ) );
			Assert.IsTrue ( strLengths.Contains( 7 ) );
			Assert.IsTrue ( strLengths.Contains( 17 ) );
		}

		[TestMethod()]
		public void SelectKeysWhereTest()
		{
			Hashtable hashtable = StaticTestInfo.testInfo.hashTableTest;
			ObjectTest obj = hashtable.SelectKeysWhere<int, ObjectTest>((i) => (ObjectTest)hashtable[i], (o)=>o.MyInt == 5).First();

			Assert.AreEqual( 5, obj.MyInt );
			Assert.AreEqual( "test", obj.TestStr );
		}

		[TestMethod()]
		public void SelectKeysWhereTest1()
		{
			Hashtable hashtable = StaticTestInfo.testInfo.hashTableTest;
			ObjectTest obj = hashtable.SelectKeysWhere<int, ObjectTest>((i) => (ObjectTest)hashtable[i], (index, o)=>((ObjectTest)hashtable[index]).MyInt == 5).First();

			Assert.AreEqual( 5, obj.MyInt );
			Assert.AreEqual( "test", obj.TestStr );
		}

		[TestMethod()]
		public void SelectManyTest()
		{
			Hashtable hashtable = StaticTestInfo.testInfo.hashTableTest;
			IEnumerable<char> strLengths = hashtable.SelectMany<string, char>(str => str.AsEnumerable());

			Assert.AreEqual( 7, strLengths.Count((c)=> c=='a'));
			Assert.AreEqual( 4, strLengths.Count((c)=> c=='s'));
		}

		[TestMethod()]
		public void SelectManyTest1()
		{
			ArrayList list = StaticTestInfo.testInfo.arrListTest;
			IEnumerable<char> strLengths = list.SelectMany<string, char>(str => str.AsEnumerable());

			Assert.AreEqual( 2, strLengths.Count( ( c ) => c=='a' ) );
			Assert.AreEqual( 1, strLengths.Count( ( c ) => c=='o' ) );
		}

		//[TestMethod()]
		//public void SkipSelectTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SkipListTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void TakeWhereTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void TakeAndRemoveTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void TryGetValueTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ContainsAnyTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SelectContainsTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SelectContainsAnyTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void FlattenTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SplitTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SplitTest1()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void GetFileEndTypeTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void IsEmptySpaceTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ScalpWhereTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ScalpWhereTest1()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void SendAsyncTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void FindTreeNodeTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToArrayListIDsTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ConvertToBagTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ConvertToBagTest1()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToArrayTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToHashSetTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToConcurrentSetTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToConcurrentSetTest1()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToArrayListTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToArrayListTest1()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToHackArrayTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToBytesTest()
		//{
		//	Assert.Fail();
		//}

		//[TestMethod()]
		//public void ToBase64StringTest()
		//{
		//	Assert.Fail();
		//}
	}
	public class TestInfo
	{
		public ObjectTest testObject = new ObjectTest();
		public ArrayList arrListTest = new ArrayList()
		{
			"abc",
			new ArrayList()
			{
				1, "test", 2, false, 3.91D
			},
			501,
			10f,
			20D,
			300.0002031M,
			"another string"
		};
		public Hashtable hashTableTest = new Hashtable()
		{
			{"apples", 450},
			{true, "just a test"},
			{2, new ObjectTest()},
			{"oranges", 1000},
			{"bananas and pears", 200}
		};
		public IEnumerable<int> intsTest = Enumerable.Range(1, 100);

		public class ObjectTest
		{
			public int MyInt { get; set; } = 5;
			public string TestStr = "test";
			public static bool TestBool = true;
		}
	}
	public static class StaticTestInfo
	{
		public static TestInfo testInfo = new TestInfo();
	}
}
