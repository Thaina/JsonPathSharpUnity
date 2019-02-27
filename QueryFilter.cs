using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace JsonPath
{
	public class QueryFilter : PathFilter
	{
		public bool Scan;
		public QueryExpression Expression { get; set; }

		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			if(Scan)
			{
				foreach(var c in current.SelectMany((item) => RecursiveJsonValue(item)))
				{
					if(Expression.IsMatch(root,c))
						yield return c;
				}
			}
			else
			{
				foreach(var items in current.OfType<IEnumerable>())
				{
					if(items is IEnumerable<char> c)
					{
						if(Expression.IsMatch(root,c))
							yield return c;

						continue;
					}

					foreach(var v in items)
					{
						if(Expression.IsMatch(root,v))
							yield return v;
					}
				}
			}
		}
	}
}