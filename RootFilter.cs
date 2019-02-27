using System.Collections.Generic;

namespace JsonPath
{
	public class RootFilter : PathFilter
	{
		public static readonly RootFilter Instance = new RootFilter();

		private RootFilter() { }
		public override IEnumerable<object> ExecuteFilter(object root,IEnumerable<object> current,bool errorWhenNoMatch)
		{
			yield return root;
		}
	}
}