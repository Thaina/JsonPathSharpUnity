using System;
using System.Collections.Generic;

namespace JsonPath
{
	public static class Ext
	{
		public static IEnumerable<object> SelectTokens(this object obj,string path,bool error = false) => new JPath(path).Evaluate(obj,obj,error);
		public static IEnumerable<object> SelectTokens(this object obj,object root,string path,bool error = false) => new JPath(path).Evaluate(root,obj,error);
	}
}