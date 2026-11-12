using System.Collections;
using System.Collections.Generic;

using HackPDM.Infrastructure.SldWrks;
using HackPDM.Shared.GlobalData;

namespace HackPDM.Core.Hack;

/// <summary>
/// The dependency tree of a local file: each node holds a <see cref="ResultHackFile"/> (the file and whether it is usable),
/// and its children are the files it references. Built from one root file, then filled by
/// <see cref="ResultNode.ProcessDependencies"/>.
/// </summary>
/// <remarks>Used by <see cref="HackPDM.Infrastructure.Hack.DependencyScanner"/> to decide which files must be committed together.</remarks>
public class HackResultTree
{
	/// <summary>A node of a <see cref="HackResultTree"/>: one file and, as list items, the files it references.</summary>
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
		/// <summary>Whether this node's own file is broken (missing, outside the working folder, or unreadable); true when the node has no value.</summary>
		public bool IsBroken => Value?.IsBroken ?? true;
		public bool IsProcessed = false;
		/// <summary>Whether this node or any node below it is broken.</summary>
		public bool IsBrokenPropagated => IsBroken || Children.Any( c => c.IsBrokenPropagated );
		/// <summary>Whether this node or any of its ancestors is broken.</summary>
		public bool IsBrokenBranchPropagated => (IsBroken || Parent?.IsBrokenBranchPropagated is true);
		/// <summary>Whether any node in the whole tree (from the topmost ancestor down) is broken.</summary>
		public bool IsBrokenTreePropagated => ( GetRoot().IsBrokenPropagated );
		/// <summary>The broken nodes of the whole tree.</summary>
		/// <value>
		/// Computed once from the root (children ask their parent) and cached; null until this node has been processed by
		/// <see cref="ProcessDependencies"/>.
		/// </value>
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
		/// <summary>The non-broken nodes of the whole tree, root included, recomputed on every read.</summary>
		/// <value>The same list as <see cref="GetAllNodes"/> on the root; despite the name it is tree-wide, not just this node's children, and leaves broken nodes out.</value>
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

		/// <summary>Creates a node with no children.</summary>
		/// <param name="rNodeValue">The file this node stands for, already tested.</param>
		/// <param name="parent">The parent node, or null for a root. The node is not added to the parent's children; use <see cref="Add"/> for that.</param>
		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent = null) 
			: this( rNodeValue, parent, [] ) {}
		/// <summary>Creates a node with the given children.</summary>
		/// <param name="rNodeValue">The file this node stands for, already tested.</param>
		/// <param name="parent">The parent node, or null for a root.</param>
		/// <param name="children">Child nodes; each is re-parented to the new node.</param>
		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent = null, params ResultNode[] children ) 
			: this( rNodeValue, parent, (IEnumerable<ResultNode>)children ) {}
		/// <summary>Creates a node with the given children.</summary>
		/// <param name="rNodeValue">The file this node stands for, already tested.</param>
		/// <param name="parent">The parent node, or null for a root. The node is not added to the parent's children.</param>
		/// <param name="children">Child nodes; each is re-parented to the new node and inherits its tree.</param>
		/// <remarks>Dependencies are resolved by awaiting <see cref="ProcessDependencies"/>, never from here.</remarks>
		public ResultNode( ResultHackFile? rNodeValue, ResultNode? parent, IEnumerable<ResultNode> children )
		{
			this.Value = rNodeValue;
			this.Parent = parent;
			this.Root = parent is not null ? ( parent?.Root ?? this ) : this;
			this.Tree ??= parent is not null ? ( parent?.Tree ) : Tree;
			this.Children = [];
			foreach( var child in children )
				Add( child );
		}


		public IEnumerator<ResultNode> GetEnumerator() => Children.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		/// <summary>Appends a child and makes this node its parent (which also gives it this node's tree and root).</summary>
		/// <param name="item">The child node.</param>
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
		/// <summary>Inserts a child at <paramref name="index"/> and makes this node its parent.</summary>
		/// <param name="index">The position among the children.</param>
		/// <param name="item">The child node.</param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside 0..<see cref="Count"/>.</exception>
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

		/// <summary>
		/// Resolves the references of this node's file into child nodes, then processes each non-broken child the same way,
		/// building the tree depth-first.
		/// </summary>
		/// <param name="stopAtBroken">When true (the default), nothing is done for a node below a broken ancestor. Children are always processed with the default.</param>
		/// <remarks>
		/// <para>
		/// Only a leaf with a SolidWorks extension is read: its references come from <see cref="HackPDM.Infrastructure.SldWrks.SwDependencies.Of"/>
		/// through one <see cref="HackPDM.Infrastructure.SldWrks.SwReferenceResolver"/> per tree, rooted at the working folder. A reference that
		/// can't be found locally gets a node for its expected path (and so is broken); a reference back to an ancestor or to itself is skipped.
		/// </para>
		/// <para>
		/// If the file's references can't be read, the node is marked broken with <see cref="HackResult.CorruptDepFile"/> instead of throwing.
		/// Nodes that are already processed or broken are left alone; a broken node is never marked processed.
		/// </para>
		/// </remarks>
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
		/// <summary>Walks this node and everything below it, breadth-first.</summary>
		/// <param name="onlyBroken">True to return only broken nodes; false (the default) to return only non-broken nodes.</param>
		/// <returns>The matching nodes, lazily, in breadth-first order. Children of broken nodes are still visited.</returns>
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
	/// <summary>Creates a tree whose root is <paramref name="file"/>, tested for existence and for being inside the working folder.</summary>
	/// <param name="file">The root file.</param>
	public HackResultTree( HackFile file ) : this( new ResultHackFile( file ) ) { }
	/// <summary>Creates a tree whose root holds an already tested file.</summary>
	/// <param name="value">The root file's test result.</param>
	public HackResultTree( ResultHackFile value ) : this( new ResultNode( value ) ) { }
	/// <summary>Creates a tree from an existing root node, attaching the node to this tree.</summary>
	/// <param name="rootNode">The root node; its Tree and Root are set to this tree and itself.</param>
	public HackResultTree( ResultNode rootNode )
	{
		Root = rootNode;
		rootNode.Tree = this;
		rootNode.Root = rootNode;
	}
	/// <summary>The root node: the file the tree was built for.</summary>
	public ResultNode Root
	{
		get;
		set;
	}
}

