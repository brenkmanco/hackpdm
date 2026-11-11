using System.Collections;
using System.Collections.Generic;

using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

public class HackResultTree
{
	public class ResultNode : IList<ResultNode>
	{
		public int Count => Children.Count;
		public bool IsReadOnly => false;
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
			this.Children = [];
			foreach( var child in children )
				Add( child );

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
		public void Insert( int index, ResultNode item )
		{
			item.Parent = this;
			Children.Insert( index, item );
		}
		public void RemoveAt( int index ) => Children.RemoveAt( index );





		// one resolver (and vault index) per tree, owned by its root
		private SwReferenceResolver? _resolver;
		private SwReferenceResolver Resolver => Parent?.Resolver ?? ( _resolver ??= new( HackDefaults.Instance.PwaPathAbsolute ) );

		/// <summary>Where a reference that can't be found is reported: its stored path on Windows, else next to the referencing file.</summary>
		private static string UnresolvedPath( string stored, string? referencing )
			=> OperatingSystem.IsWindows() || Path.GetDirectoryName( referencing ) is not { Length: > 0 } dir
				? stored
				: Path.Combine( dir, SwReferenceResolver.FileName( stored ) );

		private bool IsAncestorOrSelf( string path )
		{
			var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			for( var node = this; node is not null; node = node.Parent )
				if( node.Value?.Hack?.FullPath is { } p && string.Equals( Path.GetFullPath( p ), Path.GetFullPath( path ), comparison ) )
					return true;
			return false;
		}

		public async Task ProcessDependencies(bool stopAtBroken = true)
		{
			if( IsProcessed || IsBroken || (stopAtBroken && IsBrokenBranchPropagated) )
				return;

			if( IsLeaf )
			{
				// if parent file is not a dependent type, return clean
				if( !( this.Value?.Hack?.TypeExt is { } ext && SwDocumentTypes.IsSolidWorksExtension( ext ) ) )
				{
					IsProcessed = true;
					return;
				}

				try
				{
					string referencing = this.Value!.Hack.FullPath!;
					var references = SwDependencies.Of( referencing, Resolver );
					// if no dependencies, return clean
					if( references.Count == 0 )
					{
						IsProcessed = true;
						return;
					}

					foreach( var (stored, local) in references )
					{
						// stored references are Windows paths from the machine that saved the file
						string path = local ?? UnresolvedPath( stored, referencing );
						// circular references (e.g. in-context parts) would otherwise recurse forever
						if( IsAncestorOrSelf( path ) )
							continue;
						ResultHackFile resHack = new(HackFile.GetFromPath(path), HackTestDepth.FileExistsTest);
						// Add (not Children.Add) so the child knows its parent and tree
						Add(new ResultNode(resHack));
					}
				}
				catch( Exception e )
				{
					// an unreadable file used to count as having no references, so an assembly
					// could be committed without its parts; it now breaks the tree like a missing file
					System.Diagnostics.Debug.WriteLine( $"references of {this.Value?.Hack?.FullPath} can't be read: {e}" );
					this.Value?.MarkUnreadable();
					IsProcessed = true;
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

