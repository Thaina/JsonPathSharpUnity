using System;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public enum QueryOperator
	{
		None = 0,
		Equals = 1,
		NotEquals = 2,
		Exists = 3,
		LessThan = 4,
		LessThanOrEquals = 5,
		GreaterThan = 6,
		GreaterThanOrEquals = 7,
		And = 8,
		Or = 9,
		RegexEquals = 10,
		StrictEquals = 11,
		StrictNotEquals = 12
	}

	public abstract class QueryExpression
	{
		public QueryOperator Operator { get; set; }

		public abstract bool IsMatch(object root,object t);
	}

	public class CompositeExpression : QueryExpression
	{
		public List<QueryExpression> Expressions { get; set; }

		public CompositeExpression()
		{
			Expressions = new List<QueryExpression>();
		}

		public override bool IsMatch(object root,object t)
		{
			switch(Operator)
			{
				case QueryOperator.And:
					return !Expressions.Any((e) => !e.IsMatch(root,t));
				case QueryOperator.Or:
					return Expressions.Any((e) => e.IsMatch(root,t));
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
	}

	public class BooleanQueryExpression : QueryExpression
	{
		public object Left { get; set; }
		public object Right { get; set; }

		private IEnumerable<object> GetResult(object root,object t,object o)
		{
			if(o is IEnumerable<PathFilter> pathFilters)
			{
				foreach(var item in JPath.Evaluate(pathFilters,root,t,false))
					yield return item;
			}
			else if(o == null || o is string || o is IConvertible)
				yield return o;
		}

		public override bool IsMatch(object root,object t)
		{
			if(Operator == QueryOperator.Exists)
			{
				return GetResult(root,t,Left).Any();
			}

			using(var leftResults = GetResult(root,t,Left).GetEnumerator())
			{
				if(leftResults.MoveNext())
				{
					var rightResults = GetResult(root, t, Right);

					do
					{
						var leftResult = leftResults.Current;
						foreach(var rightResult in rightResults)
						{
							if(MatchTokens(leftResult,rightResult))
							{
								return true;
							}
						}
					} while(leftResults.MoveNext());
				}
			}

			return false;
		}

		private bool MatchTokens(object leftResult,object rightResult)
		{
			if(!(leftResult is IConvertible leftValue && rightResult is IConvertible rightValue))
			{
				switch(Operator)
				{
					case QueryOperator.Exists:
					// you can only specify primitive types in a comparison
					// notequals will always be true
					case QueryOperator.NotEquals:
						return true;
					default:
						return false;

					case QueryOperator.Equals:
					case QueryOperator.StrictEquals:
						return leftResult == rightResult;
					case QueryOperator.StrictNotEquals:
						return leftResult != rightResult;
					case QueryOperator.GreaterThan:
						return JsonCompare(leftResult,rightResult) > 0;
					case QueryOperator.GreaterThanOrEquals:
						return JsonCompare(leftResult,rightResult) >= 0;
					case QueryOperator.LessThan:
						return JsonCompare(leftResult,rightResult) < 0;
					case QueryOperator.LessThanOrEquals:
						return JsonCompare(leftResult,rightResult) <= 0;
				}
			}
			
			switch(Operator)
			{
				case QueryOperator.RegexEquals:
					return RegexEquals(leftValue,rightValue);
				case QueryOperator.Equals:
					return EqualsWithStringCoercion(leftValue,rightValue);
				case QueryOperator.StrictEquals:
					return EqualsWithStrictMatch(leftValue,rightValue);
				case QueryOperator.NotEquals:
					return !EqualsWithStringCoercion(leftValue,rightValue);
				case QueryOperator.StrictNotEquals:
					return !EqualsWithStrictMatch(leftValue,rightValue);
				case QueryOperator.GreaterThan:
					return JsonCompare(leftValue,rightValue) > 0;
				case QueryOperator.GreaterThanOrEquals:
					return JsonCompare(leftValue,rightValue) >= 0;
				case QueryOperator.LessThan:
					return JsonCompare(leftValue,rightValue) < 0;
				case QueryOperator.LessThanOrEquals:
					return JsonCompare(leftValue,rightValue) <= 0;
				case QueryOperator.Exists:
					return true;

				default:
					return false;
			}

		}
		
		private static int JsonTypeCode(object obj)
		{
			switch(obj)
			{
				case null:
					return 0;
				case bool b:
					return 1;
				case string s:
				case IEnumerable<char> c:
					return 3;
				default:
				case IDictionary d:
					return 5;
				case IEnumerable e:
					return 4;
				case IConvertible c:
					return 2;
			}
		}
		
		private static int JsonCompareString(IEnumerable<char> left,IEnumerable<char> right)
		{
			if(!(left is string l))
				l	= new string(left.ToArray());
			if(!(right is string r))
				r	= new string(right.ToArray());

			return l.CompareTo(r);
		}

		private static int JsonCompare(object left,object right)
		{
			var diffType	= JsonTypeCode(left) - JsonTypeCode(right);
			if(diffType != 0)
				return diffType;

			if(left == right)
				return 0;

			if(left is bool bl && right is bool br)
				return (bl ? 1 : 0) - (br ? 1 : 0);
				
			if(left is IEnumerable<char> sl && right is IEnumerable<char> sr)
				return JsonCompareString(sl,sr);
				
			if(left is IConvertible cl && right is IConvertible cr)
				return Math.Sign(cl.ToDecimal(null) - cr.ToDecimal(null));
				
			if(left is IDictionary dl && right is IDictionary dr)
				return dl.OfType<DictionaryEntry>().Join(dr.OfType<DictionaryEntry>(),(outer) => outer.Key,(inner) => inner.Key,(outer,inner) => (key: outer.Key,value: JsonCompare(outer,inner))).Where((pair) => pair.value != 0).OrderBy((pair) => pair.key).Select((pair) => pair.value).FirstOrDefault();

			if(left is IEnumerable el && right is IEnumerable er)
				return el.OfType<object>().Zip(er.OfType<object>(),(outer,inner) => JsonCompare(outer,inner)).FirstOrDefault((value) => value != 0);

			return 0;
		}

		private static bool RegexEquals(object input,object pattern)
		{
			if(!(input is string value && pattern is string regexText))
				return false;

			int patternOptionDelimiterIndex = regexText.LastIndexOf('/');

			string patternText = regexText.Substring(1, patternOptionDelimiterIndex - 1);
			string optionsText = regexText.Substring(patternOptionDelimiterIndex + 1);

			return Regex.IsMatch(value,patternText,GetRegexOptions(optionsText));
		}

		internal static RegexOptions GetRegexOptions(string optionsText) => optionsText.Aggregate(RegexOptions.None,(options,c) => {
			switch(c)
			{
				case 'i':
					return options | RegexOptions.IgnoreCase;
				case 'm':
					return options | RegexOptions.Multiline;
				case 's':
					return options | RegexOptions.Singleline;
				case 'x':
					return options | RegexOptions.ExplicitCapture;
				default:
					return options;
			}
		});

		internal static bool EqualsWithStringCoercion(object value,object queryValue)
		{
			if(value == null || queryValue == null)
				return value == null && queryValue == null;

			if(value?.Equals(queryValue) == true)
				return true;

			if(!(value is string currentValueString))
				currentValueString	= value.ToString();
			if(!(queryValue is string queryValueString))
				queryValueString	= queryValue.ToString();

			if(string.Equals(currentValueString,queryValueString,StringComparison.Ordinal))
				return true;

			// Handle comparing an integer with a float
			// e.g. Comparing 1 and 1.0
			if(value is IConvertible && queryValue is IConvertible)
				return decimal.TryParse(currentValueString,out var l) && decimal.TryParse(queryValueString,out var r) && l == r;

			return false;
		}

		internal static bool EqualsWithStrictMatch(object value,object queryValue)
		{
			Debug.Assert(value != null);
			Debug.Assert(queryValue != null);
			return JsonCompare(value,queryValue) == 0;
		}

		public static bool IsJsonValue(object obj)
		{
			switch(obj)
			{
				case null:
				case string s:
					return true;
				case IConvertible i:
					switch(i.GetTypeCode())
					{
						case TypeCode.DateTime:
							return false;
						default:
							return true;
					}
				default:
					return false;
			}
		}
	}
}