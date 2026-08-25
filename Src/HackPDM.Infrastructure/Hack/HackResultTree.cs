using System.Collections;
using System.Collections.Generic;

using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

public class HackResultTree
{
	public class ResultNode : IList<ResultNode>
	{
		public int Count { get; }
		public bool IsReadOnly { get; }
		public HackResultTree? Tree { get; set; }
		public ResultNode? Root 
		{
			get => field = Tree?.Root;
			set; 
		}
		public ResultNode? Parent 
		{ 
			get;
			set
			{
				field = value;
				if (field is not null)
				{
					Root = field.Root;
					Tree = field.Tree;
				}
			} 
		}
		public ResultHackFile? Value { get; set; }
		public List<ResultNode> Children { get; set; } = [];

		public bool IsRoot => Parent is null;
		public bool IsLeaf => Children.Count == 0;
		public bool IsBranch => !IsLeaf;
		public bool IsBroken => Value?.IsBroken ?? true;
		public bool IsProcessed = false;
		public bool IsBrokenPropagated => IsBroken || Children.Any( c => c.IsBrokenPropagated );
		public bool IsBrokenBranchPropagated => (IsBroken || Parent?.IsBrokenBranchPropagated is true);
		public bool IsBrokenTreePropagated => ( GetRoot().IsBrokenPropagated );
		public List<ResultNode>? BrokenNodes
		{
			get
			{
				if (IsProcessed)
				{
					field ??= IsRoot 
						? [ .. GetAllNodes( true ) ]
						: ( Parent?.BrokenNodes );
				}
				return field;
			}
		}
		public List<ResultNode>? AllChildren
		{
			get
			{
				field = IsRoot ? [ .. GetAllNodes() ] : ( Parent?.AllChildren );
				return field;
			}
		}

		public ResultNode this[ int index ]
		{
			get => Children[ index ];
			set => Children[ index ] = value;
		}

		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent = null) 
			: this( rNodeValue, parent, [] ) {}
		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent = null, params ResultNode[] children ) 
			: this( rNodeValue, parent, false, children) {}
		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent = null, bool process = false, params IEnumerable<ResultNode> children )
		{
			this.Value = rNodeValue;
			this.Parent = parent;
			this.Root = parent is not null ? ( parent?.Root ?? this ) : this;
			this.Tree ??= parent is not null ? ( parent?.Tree ) : Tree;
			this.Children = [ .. children ];

			if( process )
				ProcessDependencies();
		}


		public IEnumerator<ResultNode> GetEnumerator() => Children.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public void Add( ResultNode item )
		{
			item.Parent = this;
			Children.Add( item );
		}
		public void Clear() => Children.Clear();
		public bool Contains( ResultNode item ) => Children.Contains( item );
		public void CopyTo( ResultNode[] array, int arrayIndex ) => Children.CopyTo( array, arrayIndex );
		public bool Remove( ResultNode item ) => Children.Remove( item );

		public int IndexOf( ResultNode item ) => Children.IndexOf( item );
		public void Insert( int index, ResultNode item ) => Children.Insert( index, item );
		public void RemoveAt( int index ) => Children.RemoveAt( index );





		public async Task ProcessDependencies(bool stopAtBroken = true)
		{
			if( IsProcessed || IsBroken || (stopAtBroken && IsBrokenBranchPropagated) )
				return;

			// find all dependencies
			List<string[]>? dependencies = null;

			if( IsLeaf )
			{
				// if parent file is not a dependent type, return clean
				if( !( this.Value?.Hack?.TypeExt is { } ext && OdooDefaultsConstants.DependentExt.Contains( $"{ext}" ) ) )
				{
					IsProcessed = true;
					return;
				}

				try
				{
					dependencies = SolidWorksUtil.DocMgr?.GetDependencies( this.Value?.Hack.FullPath! );
					// if no dependencies, return clean
					if( dependencies is not { Count: > 0 } )
					{
						IsProcessed = true;
						return;
					}

					foreach( var path in dependencies.Select( deps => deps[ 1 ] ) )
					{
						ResultHackFile resHack = new(HackFile.GetFromPath(path, FileOperations.GetRelativePath(path)), HackTestDepth.FileExistsTest);
						ResultNode node = [with( resHack )];
						Children.Add(node);
					}
				}
				catch
				{
					return;
				}
			}
			
			foreach( var child in Children )
			{
				if( !child.IsBroken )
					await child.ProcessDependencies();
			}
			IsProcessed = true;
		}


		private ResultNode GetRoot()
		{
			var current = this;
			while( current.Parent != null )
			{
				current = current.Parent;
			}
			return current;
		}
		public IEnumerable<ResultNode> GetAllNodes(bool onlyBroken = false)
		{
			var queue = new Queue<ResultNode>();
			queue.Enqueue( this );

			while( queue.Count > 0 )
			{
				var current = queue.Dequeue();

				if( onlyBroken ^ !current.IsBroken )
				{
					yield return current;
				}

				if( current.Children is { Count: > 0 } )
				{
					foreach( var child in current.Children )
					{
						queue.Enqueue( child );
					}
				}
			}
		}
	}
	public HackResultTree( HackFile file ) : this( new ResultHackFile( file ) ) { }
	public HackResultTree( ResultHackFile value ) : this( new ResultNode( value ) ) { }
	public HackResultTree( ResultNode rootNode )
	{
		Root = rootNode;
		rootNode.Tree = this;
		rootNode.Root = rootNode;
	}
	public ResultNode Root
	{
		get;
		set;
	}
}

