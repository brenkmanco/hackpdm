using System.Collections;
using System.Collections.Generic;

namespace HackPDM.Core.Hack;

public class HackResultTree(ResultHackFile? value = null)
{
	public class ResultNode(ResultHackFile? rNodeValue = null) : IList<ResultNode>
	{
		public int Count { get; }
		public bool IsReadOnly { get; } 
        public ResultNode? Parent { get; set; } = null;
        public ResultHackFile? Value { get; set; } = rNodeValue;
        public List<ResultNode> Children { get; set; } = [];

		public ResultNode this[int index]
		{
			get => Children[index];
			set => Children[index] = value;
		}

		public IEnumerator<ResultNode> GetEnumerator() => Children.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public void Add(ResultNode item) => Children.Add(item);
		public void Clear() => Children.Clear();
		public bool Contains(ResultNode item) => Children.Contains(item);
		public void CopyTo(ResultNode[] array, int arrayIndex) => Children.CopyTo(array, arrayIndex);
		public bool Remove(ResultNode item) => Children.Remove(item);

		public int IndexOf(ResultNode item) => Children.IndexOf(item);
		public void Insert(int index, ResultNode item) =>  Children.Insert(index, item);
		public void RemoveAt(int index) => Children.RemoveAt(index);
	}
	public ResultNode? Root { get; set; } = new(value);
}

