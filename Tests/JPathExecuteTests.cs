#region License
// Copyright (c) 2007 James Newton-King
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using JsonPath;

using NUnit.Framework;

using JObject = System.Collections.Specialized.OrderedDictionary;
using System.Numerics;

namespace JsonPath.Tests
{
	public static class JToken
	{
		public static bool DeepEquals(object left,object right)
		{
			if(left == null || right == null)
				return left == null && right == null;

			if(left.Equals(right))
				return true;

			if(left is IDictionary || right is IDictionary)
				return left is IDictionary leftDict && right is IDictionary rightDict && leftDict.OfType<DictionaryEntry>().All((pair) => {
					return rightDict.Contains(pair.Key) && DeepEquals(pair.Value,rightDict[pair.Key]);
				});

			if(left is IEnumerable<char> || right is IEnumerable<char>)
				return left is IEnumerable<char> lc && right is IEnumerable<char> rc && !lc.Zip(rc,(l,r) => l == r).Any((eq) => !eq);

			if(left is IEnumerable || right is IEnumerable)
				return left is IEnumerable lefts && right is IEnumerable rights && !lefts.ZipForObjects(rights).Any((pair) => {
					return (pair.left.exist != pair.right.exist) || !DeepEquals(pair.left.value,pair.right.value);
				});

			if(left is IConvertible ln && right is IConvertible rn)
				return ln.ToDecimal(null) == rn.ToDecimal(null);

			return false;
		}

		static IEnumerable<((bool exist,object value) left,(bool exist,object value) right)> ZipForObjects(this IEnumerable lefts,IEnumerable rights)
		{
			var liter	= lefts?.GetEnumerator();
			var riter	= rights?.GetEnumerator();
			while(true)
			{
				var (lNext,rNext)	= (liter?.MoveNext() ?? false,riter?.MoveNext() ?? false);
				if(!lNext && !rNext)
					break;

				yield return ((lNext,lNext ? liter?.Current : null),(rNext,rNext ? riter?.Current : null));
			}
		}
	}

	public class JPathExecuteTests
	{
		[Test]
		public void GreaterThanIssue1518()
		{
			string statusJson = @"{""usingmem"": ""214376""}";//214,376

			var jObj = MiniJSON.jsonDecode(statusJson);



			var aa = new JPath("$..[?(@.usingmem>10)]").Evaluate(jObj,jObj,false);//found,10
			Assert.AreEqual(jObj,aa.FirstOrDefault());

			var bb = new JPath("$..[?(@.usingmem>27000)]").Evaluate(jObj,jObj,false);//null, 27,000
			Assert.AreEqual(jObj,bb.FirstOrDefault());

			var cc = new JPath("$..[?(@.usingmem>21437)]").Evaluate(jObj,jObj,false);//found, 21,437
			Assert.AreEqual(jObj,cc.FirstOrDefault());

			var dd = new JPath("$..[?(@.usingmem>21438)]").Evaluate(jObj,jObj,false);//null,21,438
			Assert.AreEqual(jObj,dd.FirstOrDefault());
		}
		[Test]
		public void GreaterThanWithIntegerParameterAndStringValue()
		{
			string json = @"{
  ""persons"": [
    {
      ""name""  : ""John"",
      ""age"": ""26""
    },
    {
      ""name""  : ""Jane"",
      ""age"": ""2""
    }
  ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results0 = new JPath("$.persons[?(@.age > 3)]").Evaluate(models,models,false).ToList();

			Assert.That(results0,Has.Count.EqualTo(2));
		}
		[Test]
		public void GreaterThanWithStringParameterAndIntegerValue()
		{
			string json = @"{
  ""persons"": [
    {
      ""name""  : ""John"",
      ""age"": 26
    },
    {
      ""name""  : ""Jane"",
      ""age"": 2
    }
  ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results0 = new JPath("$.persons[?(@.age > '3')]").Evaluate(models,models,false).ToList();

			Assert.That(results0,Has.Count.EqualTo(0));
		}

		[Test]
		public void RecursiveWildcard()
		{
			string json = @"{
    ""a"": [
        {
            ""id"": 1
        }
    ],
    ""b"": [
        {
            ""id"": 2
        },
        {
            ""id"": 3,
            ""c"": {
                ""id"": 4
            }
        }
    ],
    ""d"": [
        {
            ""id"": 5
        }
    ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results = new JPath("$.b..*.id").Evaluate(models,models,false).ToList();

			Assert.AreEqual(3,results.Count);
			Assert.AreEqual(2,results[0]);
			Assert.AreEqual(3,results[1]);
			Assert.AreEqual(4,results[2]);
		}

		[Test]
		public void ScanFilter()
		{
			string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results = new JPath("$.elements..[?(@.id=='AAA')]").Evaluate(models,models,false).ToList();

			Assert.AreEqual(1,results.Count);
			Assert.AreEqual(((dynamic)models)["elements"][0]["children"][0]["children"][0],results[0]);
		}

		[Test]
		public void FilterTrue()
		{
			string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results = new JPath("$.elements[?(true)]").Evaluate(models,models,false).ToList();

			Assert.AreEqual(2,results.Count);
			Assert.AreEqual(results[0],((dynamic)models)["elements"][0]);
			Assert.AreEqual(results[1],((dynamic)models)["elements"][1]);
		}

		[Test]
		public void ScanFilterTrue()
		{
			string json = @"{
  ""elements"": [
    {
      ""id"": ""A"",
      ""children"": [
        {
          ""id"": ""AA"",
          ""children"": [
            {
              ""id"": ""AAA""
            },
            {
              ""id"": ""AAB""
            }
          ]
        },
        {
          ""id"": ""AB""
        }
      ]
    },
    {
      ""id"": ""B"",
      ""children"": []
    }
  ]
}";

			var models = MiniJSON.jsonDecode(json);

			var results = new JPath("$.elements..[?(true)]").Evaluate(models,models,false).ToList();

			Assert.AreEqual(25,results.Count);
		}

		[Test]
		public void ScanQuoted()
		{
			string json = @"{
    ""Node1"": {
        ""Child1"": {
            ""Name"": ""IsMe"",
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        },
        ""My.Child.Node"": {
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        }
    },
    ""Node2"": {
        ""TargetNode"": {
            ""Prop1"": ""Val1"",
            ""Prop2"": ""Val2""
        }
    }
}";

			var models = MiniJSON.jsonDecode(json);

			int result = new JPath("$..['My.Child.Node']").Evaluate(models,models,false).Count();
			Assert.AreEqual(1,result);

			result = new JPath("..['My.Child.Node']").Evaluate(models,models,false).Count();
			Assert.AreEqual(1,result);
		}

		[Test]
		public void ScanMultipleQuoted()
		{
			string json = @"{
    ""Node1"": {
        ""Child1"": {
            ""Name"": ""IsMe"",
            ""TargetNode"": {
                ""Prop1"": ""Val1"",
                ""Prop2"": ""Val2""
            }
        },
        ""My.Child.Node"": {
            ""TargetNode"": {
                ""Prop1"": ""Val3"",
                ""Prop2"": ""Val4""
            }
        }
    },
    ""Node2"": {
        ""TargetNode"": {
            ""Prop1"": ""Val5"",
            ""Prop2"": ""Val6""
        }
    }
}";

			var models = MiniJSON.jsonDecode(json);

			var results = new JPath("$..['My.Child.Node','Prop1','Prop2']").Evaluate(models,models,false).ToList();
			Assert.Contains(((dynamic)models)["Node1"]["My.Child.Node"],results);
			Assert.Contains("Val1",results);
			Assert.Contains("Val2",results);
			Assert.Contains("Val3",results);
			Assert.Contains("Val4",results);
			Assert.Contains("Val5",results);
			Assert.Contains("Val6",results);
		}

		[Test]
		public void ParseWithEmptyArrayContent()
		{
			var json = @"{
    ""controls"": [
        {
            ""messages"": {
                ""addSuggestion"": {
                    ""en-US"": ""Add""
                }
            }
        },
        {
            ""header"": {
                ""controls"": []
            },
            ""controls"": [
                {
                    ""controls"": [
                        {
                            ""defaultCaption"": {
                                ""en-US"": ""Sort by""
                            },
                            ""sortOptions"": [
                                {
                                    ""label"": {
                                        ""en-US"": ""Name""
                                    }
                                }
                            ]
                        }
                    ]
                }
            ]
        }
    ]
}";

			var o = MiniJSON.jsonDecode(json);

			var tokens  = new JPath("$..en-US").Evaluate(o,o,false).ToList();

			Assert.AreEqual(3,tokens.Count);
			Assert.AreEqual("Add",(string)tokens[0]);
			Assert.AreEqual("Sort by",(string)tokens[1]);
			Assert.AreEqual("Name",(string)tokens[2]);
		}

		[Test]
		public void SelectTokenAfterEmptyContainer()
		{
			string json = @"{
    ""cont"": [],
    ""test"": ""no one will find me""
}";

			var o = MiniJSON.jsonDecode(json);
			var results = new JPath("$..test").Evaluate(o,o,false).ToList();

			Assert.AreEqual(1,results.Count);
			Assert.AreEqual("no one will find me",(string)results[0]);
		}

		[Test]
		public void EvaluatePropertyWithRequired()
		{
			string json = "{\"bookId\":\"1000\"}";
			var o = MiniJSON.jsonDecode(json);

			var bookId = new JPath("bookId").Evaluate(o,o,true).Single();

			Assert.AreEqual("1000",bookId);
		}

		[Test]
		public void EvaluateEmptyPropertyIndexer()
		{
			var o = new JObject() { [""] = 1 };

			var t = new JPath("['']").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(1,(int)t);
		}

		[Test]
		public void EvaluateEmptyString()
		{
			var o = new JObject() { ["Blah"] = 1 };

			var t = new JPath("").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(o,t);

			t = new JPath("['']").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(null,t);
		}

		[Test]
		public void EvaluateEmptyStringWithMatchingEmptyProperty()
		{
			var o = new JObject() { [" "] = 1 };

			var t = new JPath("[' ']").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(1,t);
		}

		[Test]
		public void EvaluateWhitespaceString()
		{
			var o = new JObject() { ["Blah"] = 1 };

			var t = new JPath(" ").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(o,t);
		}

		[Test]
		public void EvaluateDollarString()
		{
			var o = new JObject() { ["Blah"] = 1 };

			var t = new JPath("$").Evaluate(o,o,false).SingleOrDefault();
			Assert.AreEqual(o,t);
		}

		[Test]
		public void EvaluateDollarTypeString()
		{
			var o = new JObject() { ["$values"] = new int[] { 1, 2, 3 } };

			var t = new JPath("$values[1]").Evaluate(o,o,false).Single();

			Assert.AreEqual(2,(int)t);
		}

		[Test]
		public void EvaluateSingleProperty()
		{
			var o = new JObject() { ["Blah"] = 1 };

			var t = new JPath("Blah").Evaluate(o,o,false).Single();
			Assert.IsNotNull(t);
			Assert.IsInstanceOf<int>(t);
			Assert.AreEqual(1,(int)t);
		}

		[Test]
		public void EvaluateWildcardProperty()
		{
			var o = new JObject() {
				["Blah"] = 1,
				["Blah2"] = 2
			};

			var t = o.SelectTokens("$.*").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.AreEqual(1,t[0]);
			Assert.AreEqual(2,t[1]);
		}

		[Test]
		public void QuoteName()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			var t = o.SelectTokens("['Blah']").First();
			Assert.IsNotNull(t);
			Assert.IsInstanceOf<int>(t);
			Assert.AreEqual(1,t);
		}

		[Test]
		public void EvaluateMissingProperty()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			CollectionAssert.IsEmpty(o.SelectTokens("Missing[1]"));
		}

		[Test]
		public void EvaluateIndexerOnObject()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			CollectionAssert.IsEmpty(o.SelectTokens("[1]"));
		}

		[Test]
		public void EvaluateIndexerOnObjectWithError()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			Assert.Throws<ArgumentOutOfRangeException>(() => { o.SelectTokens("[1]",true).First(); },@"Index 1 not valid on JObject.");
		}

		[Test]
		public void EvaluateWildcardIndexOnObjectWithError()
		{
			var o = new JObject{
				["Blah"] = 1
			};

			Assert.Throws<Exception>(() => { o.SelectTokens("[*]",true).First(); },@"Index * not valid on JObject.");
		}

		[Test]
		public void EvaluateSliceOnObjectWithError()
		{
			var o = new JObject{
				["Blah"] = 1
			};

			Assert.Throws<Exception>(() => { o.SelectTokens("[:]",true).First(); },@"Array slice is not valid on JObject.");
		}

		[Test]
		public void EvaluatePropertyOnArray()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			CollectionAssert.IsEmpty(a.SelectTokens("BlahBlah"));
		}

		[Test]
		public void EvaluateMultipleResultsError()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			Assert.Throws<InvalidOperationException>(() => { a.SelectTokens("[0, 1]").Single(); },@"Path returned multiple tokens.");
		}

		[Test]
		public void EvaluatePropertyOnArrayWithError()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			Assert.Throws<Exception>(() => { a.SelectTokens("BlahBlah",true).First(); },@"Property 'BlahBlah' not valid on JArray.");
		}

		[Test]
		public void EvaluateNoResultsWithMultipleArrayIndexes()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			Assert.Throws<IndexOutOfRangeException>(() => { a.SelectTokens("[9,10]",true).First(); },@"Index 9 outside the bounds of JArray.");
		}

		[Test]
		public void EvaluateMissingPropertyWithError()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			Assert.Throws<Exception>(() => { o.SelectTokens("Missing",true).First(); },"Property 'Missing' does not exist on JObject.");
		}

		[Test]
		public void EvaluatePropertyWithoutError()
		{
			var o = new JObject() {
				["Blah"] = 1
			};

			Assert.AreEqual(1,o.SelectTokens("Blah",true).First());
		}

		[Test]
		public void EvaluateMissingPropertyIndexWithError()
		{
			var o = new JObject{
				["Blah"] = 1
			};

			Assert.Throws<Exception>(() => { o.SelectTokens("['Missing','Missing2']",true).First(); },"Property 'Missing' does not exist on JObject.");
		}

		[Test]
		public void EvaluateMultiPropertyIndexOnArrayWithError()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			Assert.Throws<Exception>(() => { a.SelectTokens("['Missing','Missing2']",true).First(); },"Properties 'Missing', 'Missing2' not valid on JArray.");
		}

		[Test]
		public void EvaluateArraySliceWithError()
		{
			var a = new object [] { 1, 2, 3, 4, 5 };

			Assert.Throws<Exception>(() => { a.SelectTokens("[99:]",true).First(); },"Array slice of 99 to * returned no results.");

			Assert.Throws<Exception>(() => { a.SelectTokens("[1:-19]",true).First(); },"Array slice of 1 to -19 returned no results.");

			Assert.Throws<Exception>(() => { a.SelectTokens("[:-19]",true).First(); },"Array slice of * to -19 returned no results.");

			a = new object[0];

			Assert.Throws<Exception>(() => { a.SelectTokens("[:]",true).First(); },"Array slice of * to * returned no results.");
		}

		[Test]
		public void EvaluateOutOfBoundsIndxer()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			CollectionAssert.IsEmpty(a.SelectTokens("[1000].Ha"));
		}

		[Test]
		public void EvaluateArrayOutOfBoundsIndxerWithError()
		{
			var a = new[] { 1, 2, 3, 4, 5 };

			Assert.Throws<IndexOutOfRangeException>(() => { a.SelectTokens("[1000].Ha",true).First(); },"Index 1000 outside the bounds of JArray.");
		}

		[Test]
		public void EvaluateArray()
		{
			var a = new[] { 1, 2, 3, 4 };

			var t = a.SelectTokens("[1]").First();
			Assert.IsNotNull(t);
			Assert.IsInstanceOf<int>(t);
			Assert.AreEqual(2,(int)t);
		}

		[Test]
		public void EvaluateArraySlice()
		{
			var a = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
			var t = a.SelectTokens("[-3:]").ToList();
			Assert.AreEqual(3,t.Count);
			Assert.AreEqual(7,(int)t[0]);
			Assert.AreEqual(8,(int)t[1]);
			Assert.AreEqual(9,(int)t[2]);

			t = a.SelectTokens("[-1:-2:-1]").ToList();
			Assert.AreEqual(1,t.Count);
			Assert.AreEqual(9,(int)t[0]);

			t = a.SelectTokens("[-2:-1]").ToList();
			Assert.AreEqual(1,t.Count);
			Assert.AreEqual(8,(int)t[0]);

			t = a.SelectTokens("[1:1]").ToList();
			Assert.AreEqual(0,t.Count);

			t = a.SelectTokens("[1:2]").ToList();
			Assert.AreEqual(1,t.Count);
			Assert.AreEqual(2,(int)t[0]);

			t = a.SelectTokens("[::-1]").ToList();
			Assert.AreEqual(9,t.Count);
			Assert.AreEqual(9,(int)t[0]);
			Assert.AreEqual(8,(int)t[1]);
			Assert.AreEqual(7,(int)t[2]);
			Assert.AreEqual(6,(int)t[3]);
			Assert.AreEqual(5,(int)t[4]);
			Assert.AreEqual(4,(int)t[5]);
			Assert.AreEqual(3,(int)t[6]);
			Assert.AreEqual(2,(int)t[7]);
			Assert.AreEqual(1,(int)t[8]);

			t = a.SelectTokens("[::-2]").ToList();
			Assert.AreEqual(5,t.Count);
			Assert.AreEqual(9,(int)t[0]);
			Assert.AreEqual(7,(int)t[1]);
			Assert.AreEqual(5,(int)t[2]);
			Assert.AreEqual(3,(int)t[3]);
			Assert.AreEqual(1,(int)t[4]);
		}

		[Test]
		public void EvaluateWildcardArray()
		{
			var a = new[] { 1, 2, 3, 4 };

			var t = a.SelectTokens("[*]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(4,t.Count);
			Assert.AreEqual(1,(int)t[0]);
			Assert.AreEqual(2,(int)t[1]);
			Assert.AreEqual(3,(int)t[2]);
			Assert.AreEqual(4,(int)t[3]);
		}

		[Test]
		public void EvaluateArrayMultipleIndexes()
		{
			var a = new[] { 1, 2, 3, 4 };

			var t = a.SelectTokens("[1,2,0]");
			Assert.IsNotNull(t);
			Assert.AreEqual(3,t.Count());
			Assert.AreEqual(2,(int)t.ElementAt(0));
			Assert.AreEqual(3,(int)t.ElementAt(1));
			Assert.AreEqual(1,(int)t.ElementAt(2));
		}

		[Test]
		public void EvaluateScan()
		{
			var o1 = new JObject { { "Name", 1 } };
			var o2 = new JObject { { "Name", 2 } };
			var a = new[] { o1, o2 };

			var t = a.SelectTokens("$..Name").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.AreEqual(1,(int)t[0]);
			Assert.AreEqual(2,(int)t[1]);
		}

		[Test]
		public void EvaluateWildcardScan()
		{
			var o1 = new JObject { { "Name", 1 } };
			var o2 = new JObject { { "Name", 2 } };
			var a = new[] { o1, o2 };

			var t = a.SelectTokens("$..*").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(5,t.Count);
			Assert.IsTrue(JToken.DeepEquals(a,t[0]));
			Assert.IsTrue(JToken.DeepEquals(o1,t[1]));
			Assert.AreEqual(1,(int)t[2]);
			Assert.IsTrue(JToken.DeepEquals(o2,t[3]));
			Assert.AreEqual(2,(int)t[4]);
		}

		[Test]
		public void EvaluateScanNestResults()
		{
			var o1 = new JObject { { "Name", 1 } };
			var o2 = new JObject { { "Name", 2 } };
			var o3 = new JObject { { "Name", new JObject { ["Name"] = new[] { 3 } } } };
			var a = new[] { o1, o2, o3 };

			var t = a.SelectTokens("$..Name").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(4,t.Count);
			Assert.AreEqual(1,(int)t[0]);
			Assert.AreEqual(2,(int)t[1]);
			Assert.IsTrue(JToken.DeepEquals(new JObject { ["Name"] = new[] { 3 } },t[2]));
			Assert.IsTrue(JToken.DeepEquals(new[] { 3 },t[3]));
		}

		[Test]
		public void EvaluateWildcardScanNestResults()
		{
			var o1 = new JObject { { "Name", 1 } };
			var o2 = new JObject { { "Name", 2 } };
			var o3 = new JObject { { "Name", new JObject { { "Name", new[] { 3 } } } } };
			var a = new[] { o1, o2, o3 };

			var t = a.SelectTokens("$..*").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(9,t.Count);

			Assert.IsTrue(JToken.DeepEquals(a,t[0]));
			Assert.IsTrue(JToken.DeepEquals(o1,t[1]));
			Assert.AreEqual(1,(int)t[2]);
			Assert.IsTrue(JToken.DeepEquals(o2,t[3]));
			Assert.AreEqual(2,(int)t[4]);
			Assert.IsTrue(JToken.DeepEquals(o3,t[5]));
			Assert.IsTrue(JToken.DeepEquals(new JObject { ["Name"] = new[] { 3 } },t[6]));
			Assert.IsTrue(JToken.DeepEquals(new[] { 3 },t[7]));
			Assert.AreEqual(3,(int)t[8]);
		}

		[Test]
		public void EvaluateSinglePropertyReturningArray()
		{
			var o = new JObject() {
				["Blah"] =  new[] { 1, 2, 3 }
			};

			var t = o.SelectTokens("Blah").First();
			Assert.IsNotNull(t);
			Assert.IsInstanceOf<IEnumerable>(t);

			t = o.SelectTokens("Blah[2]").First();
			Assert.IsInstanceOf<int>(t);
			Assert.AreEqual(3,t);
		}

		[Test]
		public void EvaluateLastSingleCharacterProperty()
		{
			var o2 = MiniJSON.jsonDecode(@"{""People"":[{""N"":""Jeff""}]}");
			string a2 = (string)o2.SelectTokens("People[0].N").First();

			Assert.AreEqual("Jeff",a2);
		}

		[Test]
		public void ExistsQuery()
		{
			var a = new[] { new JObject() {["hi"] =  "ho" }, new JObject() { ["hi2"] = "ha" } };

			var t = a.SelectTokens("[ ?( @.hi ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(1,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = "ho" },t[0]));
		}

		[Test]
		public void EqualsQuery()
		{
			var a = new [] {
				new JObject() {["hi"] =  "ho"},
				new JObject() { ["hi"] = "ha"}
			};

			var t = a.SelectTokens("[ ?( @.['hi'] == 'ha' ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(1,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = "ha" },t[0]));
		}

		[Test]
		public void NotEqualsQuery()
		{
			var a = new [] {
				new[] { new JObject() {["hi"] =  "ho" } },
				new[] { new JObject() {["hi"] =  "ha" } }
			};

			var t = a.SelectTokens("[ ?( @..hi <> 'ha' ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(1,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new[] { new JObject() { ["hi"] = "ho" } },t[0]));
		}

		[Test]
		public void NoPathQuery()
		{
			var a = new[] { 1, 2, 3 };

			var t = a.SelectTokens("[ ?( @ > 1 ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.AreEqual(2,(int)t[0]);
			Assert.AreEqual(3,(int)t[1]);
		}

		[Test]
		public void MultipleQueries()
		{
			var a = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

			// json path does item based evaluation - http://www.sitepen.com/blog/2008/03/17/jsonpath-support/
			// first query resolves array to ints
			// int has no children to query
			var t = a.SelectTokens("[?(@ <> 1)][?(@ <> 4)][?(@ < 7)]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(0,t.Count);
		}

		[Test]
		public void GreaterQuery()
		{
			var a = new [] {
				new JObject() { ["hi"] = 1 },
				new JObject() { ["hi"] = 2 },
				new JObject() { ["hi"] = 3 }
			};

			var t = a.SelectTokens("[ ?( @.hi > 1 ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 2 },t[0]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 3 },t[1]));
		}

		[Test]
		public void LesserQuery_ValueFirst()
		{
			var a = new [] {
				new JObject() { ["hi"] = 1 },
				new JObject() { ["hi"] = 2 },
				new JObject() { ["hi"] = 3 }
			};

			var t = a.SelectTokens("[ ?( 1 < @.hi ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 2 },t[0]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 3 },t[1]));
		}

#if !(PORTABLE || DNXCORE50 || PORTABLE40 || NET35 || NET20) || NETSTANDARD1_3 || NETSTANDARD2_0
		[Test]
		public void GreaterQueryBigInteger()
		{
			var a = new [] {
				new JObject() { ["hi"] = new BigInteger(1) },
				new JObject() { ["hi"] = new BigInteger(2) },
				new JObject() { ["hi"] = new BigInteger(3) }
			};

			var t = a.SelectTokens("[ ?( @.hi > 1 ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 2 },t[0]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 3 },t[1]));
		}
#endif

		[Test]
		public void GreaterOrEqualQuery()
		{
			var a = new [] {
				new JObject() { ["hi"] = 1 },
				new JObject() { ["hi"] = 2 },
				new JObject() { ["hi"] = 2.0 },
				new JObject() { ["hi"] = 3 }
			};

			var t = a.SelectTokens("[ ?( @.hi >= 1 ) ]").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(4,t.Count);
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 1 },t[0]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 2 },t[1]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 2.0 },t[2]));
			Assert.IsTrue(JToken.DeepEquals(new JObject() { ["hi"] = 3 },t[3]));
		}

		[Test]
		public void NestedQuery()
		{
			var a = new [] {
				new JObject() {
					["name"] =  "Bad Boys",
					["cast"] = new [] {
						new JObject() {["name"] =  "Will Smith"}}},
				new JObject() {
					["name"] = "Independence Day",
					["cast"] = new [] {
						new JObject() {["name"] =  "Will Smith"}}
					},
				new JObject() {
					["name"] = "The Rock",
					["cast"] = new [] {
						new JObject() {["name"] =  "Nick Cage"}}
					}
									};

			var t = a.SelectTokens("[?(@.cast[?(@.name=='Will Smith')])].name").ToList();
			Assert.IsNotNull(t);
			Assert.AreEqual(2,t.Count);
			Assert.AreEqual("Bad Boys",(string)t[0]);
			Assert.AreEqual("Independence Day",(string)t[1]);
		}

		[Test]
		public void MultiplePaths()
		{
			var a = MiniJSON.jsonDecode(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]") as IList;

			Assert.IsInstanceOf<IList>(a);

			var results = a.SelectTokens("[?(@.price > @.max_price)]").ToList();
			Assert.AreEqual(1,results.Count);
			Assert.AreEqual(a[2],results[0]);
		}

		[Test]
		public void Exists_True()
		{
			var a = MiniJSON.jsonDecode(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]") as IList;

			Assert.IsInstanceOf<IList>(a);

			var results = a.SelectTokens("[?(true)]").ToList();
			Assert.AreEqual(3,results.Count);
			Assert.AreEqual(a[0],results[0]);
			Assert.AreEqual(a[1],results[1]);
			Assert.AreEqual(a[2],results[2]);
		}

		[Test]
		public void Exists_Null()
		{
			var a = MiniJSON.jsonDecode(@"[
  {
    ""price"": 199,
    ""max_price"": 200
  },
  {
    ""price"": 200,
    ""max_price"": 200
  },
  {
    ""price"": 201,
    ""max_price"": 200
  }
]") as IList;

			Assert.IsInstanceOf<IList>(a);

			var results = a.SelectTokens("[?(true)]").ToList();
			Assert.AreEqual(3,results.Count);
			Assert.AreEqual(a[0],results[0]);
			Assert.AreEqual(a[1],results[1]);
			Assert.AreEqual(a[2],results[2]);
		}

		[Test]
		public void WildcardWithProperty()
		{
			var o = MiniJSON.jsonDecode(@"{
    ""station"": 92000041000001, 
    ""containers"": [
        {
            ""id"": 1,
            ""text"": ""Sort system"",
            ""containers"": [
                {
                    ""id"": ""2"",
                    ""text"": ""Yard 11""
                },
                {
                    ""id"": ""92000020100006"",
                    ""text"": ""Sort yard 12""
                },
                {
                    ""id"": ""92000020100005"",
                    ""text"": ""Yard 13""
                } 
            ]
        }, 
        {
            ""id"": ""92000020100011"",
            ""text"": ""TSP-1""
        }, 
        {
            ""id"":""92000020100007"",
            ""text"": ""Passenger 15""
        }
    ]
}");

			var tokens = o.SelectTokens("$..*[?(@.text)]").OfType<IDictionary>().ToList();
			Assert.That(tokens,Has.Count.EqualTo(6));
			Assert.That(tokens.Select((dict) => dict["text"]),Is.EquivalentTo(new[]{
				"Sort system","TSP-1","Passenger 15","Yard 11","Sort yard 12","Yard 13"
			}));
		}

		[Test]
		public void Example()
		{
			var o = MiniJSON.jsonDecode(@"{
        ""Stores"": [
          ""Lambton Quay"",
          ""Willis Street""
        ],
        ""Manufacturers"": [
          {
            ""Name"": ""Acme Co"",
            ""Products"": [
              {
                ""Name"": ""Anvil"",
                ""Price"": 50
              }
            ]
          },
          {
            ""Name"": ""Contoso"",
            ""Products"": [
              {
                ""Name"": ""Elbow Grease"",
                ""Price"": 99.95
              },
              {
                ""Name"": ""Headlight Fluid"",
                ""Price"": 4
              }
            ]
          }
        ]
      }") as IDictionary;

			string name = (string)o.SelectTokens("Manufacturers[0].Name").First();
			// Acme Co

			decimal productPrice = Convert.ToDecimal(o.SelectTokens("Manufacturers[0].Products[0].Price").First());
			// 50

			string productName = o.SelectTokens("Manufacturers[1].Products[0].Name").First().ToString();
			// Elbow Grease

			Assert.AreEqual("Acme Co",name);
			Assert.AreEqual(50m,productPrice);
			Assert.AreEqual("Elbow Grease",productName);

			IList<string> storeNames = o.SelectTokens("Stores").OfType<IEnumerable>().First().OfType<string>().ToList();
			// Lambton Quay
			// Willis Street

			IList<string> firstProductNames = (o["Manufacturers"] as IEnumerable).OfType<object>().Select(m => m.SelectTokens("Products[1].Name").FirstOrDefault() as string).ToList();
			// null
			// Headlight Fluid

			decimal totalPrice = (o["Manufacturers"] as IEnumerable).OfType<object>().Sum(m => Convert.ToDecimal(m.SelectTokens("Products[0].Price").First()));
			// 149.95

			Assert.AreEqual(2,storeNames.Count);
			Assert.AreEqual("Lambton Quay",storeNames[0]);
			Assert.AreEqual("Willis Street",storeNames[1]);
			Assert.AreEqual(2,firstProductNames.Count);
			Assert.AreEqual(null,firstProductNames[0]);
			Assert.AreEqual("Headlight Fluid",firstProductNames[1]);
			Assert.AreEqual(149.95m,totalPrice);
		}

		[Test]
		public void NotEqualsAndNonPrimativeValues()
		{
			string json = @"[
  {
    ""name"": ""string"",
    ""value"": ""aString""
  },
  {
    ""name"": ""number"",
    ""value"": 123
  },
  {
    ""name"": ""array"",
    ""value"": [
      1,
      2,
      3,
      4
    ]
  },
  {
    ""name"": ""object"",
    ""value"": {
      ""1"": 1
    }
  }
]";

			var a = MiniJSON.jsonDecode(json);

			var result = a.SelectTokens("$.[?(@.value!=123)]").ToList();
			Assert.AreEqual(3,result.Count);

			result = a.SelectTokens("$.[?(@.value!=1)]").ToList();
			Assert.AreEqual(4,result.Count);

			result = a.SelectTokens("$.[?(@.value!='2000-12-05T05:07:59-10:00')]").ToList();
			Assert.AreEqual(4,result.Count);

			result = a.SelectTokens("$.[?(@.value!=null)]").ToList();
			Assert.AreEqual(4,result.Count);

			result = a.SelectTokens("$.[?(@.value)]").ToList();
			Assert.AreEqual(4,result.Count);
		}

		[Test]
		public void RootInFilter()
		{
			string json = @"[
   {
      ""store"" : {
         ""book"" : [
            {
               ""category"" : ""reference"",
               ""author"" : ""Nigel Rees"",
               ""title"" : ""Sayings of the Century"",
               ""price"" : 8.95
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""Evelyn Waugh"",
               ""title"" : ""Sword of Honour"",
               ""price"" : 12.99
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""Herman Melville"",
               ""title"" : ""Moby Dick"",
               ""isbn"" : ""0-553-21311-3"",
               ""price"" : 8.99
            },
            {
               ""category"" : ""fiction"",
               ""author"" : ""J. R. R. Tolkien"",
               ""title"" : ""The Lord of the Rings"",
               ""isbn"" : ""0-395-19395-8"",
               ""price"" : 22.99
            }
         ],
         ""bicycle"" : {
            ""color"" : ""red"",
            ""price"" : 19.95
         }
      },
      ""expensive"" : 10
   }
]";

			var a = MiniJSON.jsonDecode(json);

			var result = a.SelectTokens("$.[?($.[0].store.bicycle.price < 20)]").ToList();
			Assert.AreEqual(1,result.Count);

			result = a.SelectTokens("$.[?($.[0].store.bicycle.price < 10)]").ToList();
			Assert.AreEqual(0,result.Count);
		}

		[Test]
		public void RootInFilterWithRootObject()
		{
			string json = @"{
                ""store"" : {
                    ""book"" : [
                        {
                            ""category"" : ""reference"",
                            ""author"" : ""Nigel Rees"",
                            ""title"" : ""Sayings of the Century"",
                            ""price"" : 8.95
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""Evelyn Waugh"",
                            ""title"" : ""Sword of Honour"",
                            ""price"" : 12.99
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""Herman Melville"",
                            ""title"" : ""Moby Dick"",
                            ""isbn"" : ""0-553-21311-3"",
                            ""price"" : 8.99
                        },
                        {
                            ""category"" : ""fiction"",
                            ""author"" : ""J. R. R. Tolkien"",
                            ""title"" : ""The Lord of the Rings"",
                            ""isbn"" : ""0-395-19395-8"",
                            ""price"" : 22.99
                        }
                    ],
                    ""bicycle"" : [
                        {
                            ""color"" : ""red"",
                            ""price"" : 19.95
                        }
                    ]
                },
                ""expensive"" : 10
            }";

			var a = MiniJSON.jsonDecode(json);

			var result = a.SelectTokens("$..book[?(@.price <= $['expensive'])]").ToList();
			Assert.AreEqual(2,result.Count);

			result = a.SelectTokens("$.store..[?(@.price > $.expensive)]").ToList();
			Assert.AreEqual(3,result.Count);
		}

		[Test]
		public void RootInFilterWithInitializers()
		{
			JObject rootObject = new JObject
			{
				{ "referenceDate", DateTime.MinValue },
				{
					"dateObjectsArray",
					new[] {
						new JObject { { "date", DateTime.MinValue } },
						new JObject { { "date", DateTime.MaxValue } },
						new JObject { { "date", DateTime.Now } },
						new JObject { { "date", DateTime.MinValue } },
					}
				}
								};

			var result = rootObject.SelectTokens("$.dateObjectsArray[?(@.date == $.referenceDate)]").ToList();
			Assert.AreEqual(2,result.Count);
		}

		[Test]
		public void IdentityOperator()
		{
			var o = MiniJSON.jsonDecode(@"{
	            ""Values"": [{
				""Coercible"": 1,
                    ""Name"": ""Number""



				}, {
		            ""Coercible"": ""1"",
		            ""Name"": ""String""
	            }]
            }");

			// just to verify expected behavior hasn't changed
			IEnumerable<string> sanity1 = o.SelectTokens("Values[?(@.Coercible == '1')].Name").Select(x => (string)x);
			IEnumerable<string> sanity2 = o.SelectTokens("Values[?(@.Coercible != '1')].Name").Select(x => (string)x);
			// new behavior
			IEnumerable<string> mustBeNumber1 = o.SelectTokens("Values[?(@.Coercible === 1)].Name").Select(x => (string)x);
			IEnumerable<string> mustBeString1 = o.SelectTokens("Values[?(@.Coercible !== 1)].Name").Select(x => (string)x);
			IEnumerable<string> mustBeString2 = o.SelectTokens("Values[?(@.Coercible === '1')].Name").Select(x => (string)x);
			IEnumerable<string> mustBeNumber2 = o.SelectTokens("Values[?(@.Coercible !== '1')].Name").Select(x => (string)x);

			// FAILS-- JPath returns { "String" }
			//CollectionAssert.AreEquivalent(new[] { "Number", "String" }, sanity1);
			// FAILS-- JPath returns { "Number" }
			//Assert.IsTrue(!sanity2.Any());
			Assert.AreEqual("Number",mustBeNumber1.Single());
			Assert.AreEqual("String",mustBeString1.Single());
			Assert.AreEqual("Number",mustBeNumber2.Single());
			Assert.AreEqual("String",mustBeString2.Single());
		}

		[Test]
		public void Equals_FloatWithInt()
		{
			var t = MiniJSON.jsonDecode(@"{
  ""Values"": [
    {
      ""Property"": 1
    }
  ]
}");

			Assert.IsNotNull(t.SelectTokens(@"Values[?(@.Property == 1.0)]").First());
		}

		[TestCaseSource(nameof(StrictMatchWithInverseTestData))]
		public static void EqualsStrict(string value1,string value2,bool matchStrict)
		{
			string completeJson = @"{
  ""Values"": [
    {
      ""Property"": " + value1.Replace("'","\"") + @"
    }
  ]
}";
			string completeEqualsStrictPath = "$.Values[?(@.Property === " + value2 + ")]";
			string completeNotEqualsStrictPath = "$.Values[?(@.Property !== " + value2 + ")]";

			var t = MiniJSON.jsonDecode(completeJson);

			bool hasEqualsStrict = t.SelectTokens(completeEqualsStrictPath).Any();
			Assert.AreEqual(
				matchStrict,
				hasEqualsStrict,
				$"Expected {value1} and {value2} to match: {matchStrict}"
				+ Environment.NewLine + completeJson + Environment.NewLine + completeEqualsStrictPath);

			bool hasNotEqualsStrict = t.SelectTokens(completeNotEqualsStrictPath).Any();
			Assert.AreNotEqual(
				matchStrict,
				hasNotEqualsStrict,
				$"Expected {value1} and {value2} to match: {!matchStrict}"
				+ Environment.NewLine + completeJson + Environment.NewLine + completeNotEqualsStrictPath);
		}

		public static IEnumerable<object[]> StrictMatchWithInverseTestData()
		{
			foreach(var item in StrictMatchTestData())
			{
				yield return new object[] { item[0],item[1],item[2] };

				if(!item[0].Equals(item[1]))
				{
					// Test the inverse
					yield return new object[] { item[1],item[0],item[2] };
				}
			}
		}

		private static IEnumerable<object[]> StrictMatchTestData()
		{
			yield return new object[] { "1","1",true };
			yield return new object[] { "1","1.0",true };
			yield return new object[] { "1","true",false };
			yield return new object[] { "1","'1'",false };
			yield return new object[] { "'1'","'1'",true };
			yield return new object[] { "false","false",true };
			yield return new object[] { "true","false",false };
			yield return new object[] { "1","1.1",false };
			yield return new object[] { "1","null",false };
			yield return new object[] { "null","null",true };
			yield return new object[] { "null","'null'",false };
		}
	}
}